using System.ComponentModel;
using System.Text.Json;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetTypeOverviewTool
{
    [McpServerTool(Name = "get_type_overview")]
    [Description("Compound tool: returns a type's public API, hierarchy, implementations, and diagnostics in a single call.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the type to analyze")] string typeName,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, typeName, ct: ct);
        if (symbol is not INamedTypeSymbol typeSymbol)
            return JsonSerializer.Serialize(new { error = $"Type '{typeName}' not found" });

        // Public API
        var members = typeSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public)
            .Where(m => !m.IsImplicitlyDeclared)
            .Where(m => m is not IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
            .Select(m =>
            {
                var kind = m switch
                {
                    IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
                    IMethodSymbol => "method",
                    IPropertySymbol => "property",
                    IFieldSymbol => "field",
                    IEventSymbol => "event",
                    INamedTypeSymbol => "type",
                    _ => m.Kind.ToString().ToLowerInvariant()
                };
                var returnType = m switch
                {
                    IMethodSymbol ms => ms.ReturnType.ToDisplayString(),
                    IPropertySymbol ps => ps.Type.ToDisplayString(),
                    IFieldSymbol fs => fs.Type.ToDisplayString(),
                    _ => null
                };
                return new ApiMember(m.Name, kind, m.ToDisplayString(), returnType);
            }).ToList();
        var api = new PublicApiResult(typeSymbol.ToDisplayString(), members, members.Count);

        // Hierarchy
        var (file, line) = SymbolResolver.GetLocation(typeSymbol);
        var typeNode = new TypeHierarchyNode(
            typeSymbol.ToDisplayString(),
            typeSymbol.TypeKind.ToString().ToLowerInvariant(),
            file, line);

        var baseTypes = new List<TypeHierarchyNode>();
        var current = typeSymbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            var (bf, bl) = SymbolResolver.GetLocation(current);
            baseTypes.Add(new TypeHierarchyNode(current.ToDisplayString(),
                current.TypeKind.ToString().ToLowerInvariant(), bf, bl));
            current = current.BaseType;
        }

        var interfaces = typeSymbol.AllInterfaces.Select(i =>
        {
            var (iF, iL) = SymbolResolver.GetLocation(i);
            return new TypeHierarchyNode(i.ToDisplayString(), "interface", iF, iL);
        }).ToList();

        var solution = workspace.GetSolution()!;
        var derivedSymbols = await SymbolFinder.FindDerivedClassesAsync(typeSymbol, solution, cancellationToken: ct);
        var derivedTypes = derivedSymbols.Select(d =>
        {
            var (dF, dL) = SymbolResolver.GetLocation(d);
            return new TypeHierarchyNode(d.ToDisplayString(),
                d.TypeKind.ToString().ToLowerInvariant(), dF, dL);
        }).ToList();

        var hierarchy = new TypeHierarchyResult(typeNode, baseTypes, interfaces, derivedTypes);

        // Implementations (for interfaces)
        ImplementationsResult? implementations = null;
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            var impls = await SymbolFinder.FindImplementationsAsync(typeSymbol, solution, cancellationToken: ct);
            var implList = impls.Select(i =>
            {
                var (iFile, iLine) = SymbolResolver.GetLocation(i);
                return new ImplementationMatch(
                    i.ToDisplayString(),
                    i.TypeKind.ToString().ToLowerInvariant(),
                    iFile, iLine, i.ContainingAssembly?.Name);
            }).ToList();
            implementations = new ImplementationsResult(typeSymbol.ToDisplayString(), implList, implList.Count);
        }

        // Diagnostics for the file
        DiagnosticsResult? diagnostics = null;
        var syntaxRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is not null)
        {
            var project = solution.Projects
                .FirstOrDefault(p => p.Documents.Any(d => d.FilePath == syntaxRef.SyntaxTree.FilePath));
            if (project is not null)
            {
                var compilation = await workspace.GetCompilationAsync(project, ct);
                if (compilation is not null)
                {
                    var diags = compilation.GetDiagnostics(ct)
                        .Where(d => d.Location.SourceTree?.FilePath == syntaxRef.SyntaxTree.FilePath)
                        .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                        .Select(d => new DiagnosticInfo(
                            d.Id,
                            d.Severity.ToString(),
                            d.GetMessage(),
                            d.Location.SourceTree?.FilePath,
                            d.Location.GetLineSpan().StartLinePosition.Line + 1))
                        .Take(50)
                        .ToList();
                    diagnostics = new DiagnosticsResult(diags, diags.Count, "file");
                }
            }
        }

        var result = new TypeOverview(api, hierarchy, implementations, diagnostics);
        return JsonSerializer.Serialize(result);
    }
}
