using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

/// <summary>
/// Finds symbols with degree 0 in the solution graph — no incoming references from
/// solution code AND no structural outgoing references to other solution-defined types.
/// This is distinct from dead code (which has 0 incoming but may still reference other types).
/// </summary>
[McpServerToolType]
public static class FindIsolatedSymbolsTool
{
    [McpServerTool(Name = "find_isolated_symbols")]
    [Description("Finds types with no incoming references from the solution AND no structural dependencies on other solution types (degree 0). These are fully disconnected components — distinct from dead code, which may still reference other types.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S107:Methods should not have too many parameters", Justification = "MCP tool parameters are protocol-mandated")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (substring match)")] string? projectFilter = null,
        [Description("Include public types in analysis (default false — public types are often consumed externally)")] bool includePublicTypes = false,
        [Description("Maximum number of results to return (default 50)")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var projects = solution.Projects
            .Where(p => projectFilter is null || p.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Collect all solution type names for outgoing-edge checks
        var solutionTypeNames = await CollectSolutionTypeNamesAsync(workspace, projects, ct);

        var isolated = new List<IsolatedSymbolEntry>();

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            if (isolated.Count >= maxResults) break;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                if (isolated.Count >= maxResults) break;

                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (isolated.Count >= maxResults) break;
                    ct.ThrowIfCancellationRequested();

                    if (model.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol type) continue;
                    if (IsSystemType(type)) continue;
                    if (ShouldSkip(type, includePublicTypes)) continue;

                    // Check outgoing structural edges first (cheap — no Roslyn search)
                    if (HasSolutionTypeReferences(type, solutionTypeNames)) continue;

                    // Check incoming references (expensive — SymbolFinder)
                    var refs = await SymbolFinder.FindReferencesAsync(type, solution, ct);
                    var incomingCount = refs.Sum(r => r.Locations.Count());
                    if (incomingCount > 0) continue;

                    var loc = SymbolResolver.GetLocation(type);
                    isolated.Add(new IsolatedSymbolEntry(
                        type.ToDisplayString(),
                        type.TypeKind.ToString(),
                        loc.FilePath,
                        loc.Line,
                        project.Name));
                }
            }
        }

        var result = new IsolatedSymbolsResult(isolated, isolated.Count);
        return solution.Projects.Count() > 1
            ? WorkspaceManager.SerializeWithMultiSolutionHint(result)
            : Json.Serialize(result);
    }

    private static async Task<HashSet<string>> CollectSolutionTypeNamesAsync(
        WorkspaceManager workspace,
        List<Project> projects,
        CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var model = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(typeDecl, ct) is INamedTypeSymbol type)
                        names.Add(type.ToDisplayString());
                }
            }
        }

        return names;
    }

    private static bool HasSolutionTypeReferences(INamedTypeSymbol type, HashSet<string> solutionTypes)
    {
        if (type.BaseType is not null && solutionTypes.Contains(type.BaseType.ToDisplayString()))
            return true;

        foreach (var iface in type.Interfaces)
            if (solutionTypes.Contains(iface.ToDisplayString()))
                return true;

        foreach (var member in type.GetMembers())
        {
            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                _ => null
            };

            if (memberType is INamedTypeSymbol named && solutionTypes.Contains(named.ToDisplayString()))
                return true;
        }

        return false;
    }

    private static bool ShouldSkip(INamedTypeSymbol type, bool includePublicTypes)
    {
        // Skip compiler-generated, anonymous, and special types
        if (type.IsImplicitlyDeclared || type.IsAnonymousType) return true;
        if (type.TypeKind is TypeKind.Delegate or TypeKind.Enum) return true;

        if (!includePublicTypes && type.DeclaredAccessibility == Accessibility.Public)
            return true;

        // Skip types decorated with attributes that imply external consumption
        return type.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "McpServerToolTypeAttribute" or "ApiControllerAttribute"
                or "ControllerAttribute" or "SerializableAttribute");
    }

    private static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal));
    }
}
