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

        var solutionTypeNames = await CollectSolutionTypeNamesAsync(workspace, projects, ct);
        var isolated = new List<IsolatedSymbolEntry>();

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            if (isolated.Count >= maxResults) break;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            await AnalyzeProjectAsync(compilation, project, solution, solutionTypeNames, includePublicTypes, maxResults, isolated, ct);
        }

        var result = new IsolatedSymbolsResult(isolated, isolated.Count);
        return solution.Projects.Count() > 1
            ? WorkspaceManager.SerializeWithMultiSolutionHint(result)
            : Json.Serialize(result);
    }

    private static async Task AnalyzeProjectAsync(
        Compilation compilation,
        Project project,
        Solution solution,
        HashSet<string> solutionTypeNames,
        bool includePublicTypes,
        int maxResults,
        List<IsolatedSymbolEntry> isolated,
        CancellationToken ct)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            if (isolated.Count >= maxResults) break;

            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(ct);

            await AnalyzeTreeAsync(root, model, project, solution, solutionTypeNames, includePublicTypes, maxResults, isolated, ct);
        }
    }

    private static async Task AnalyzeTreeAsync(
        SyntaxNode root,
        SemanticModel model,
        Project project,
        Solution solution,
        HashSet<string> solutionTypeNames,
        bool includePublicTypes,
        int maxResults,
        List<IsolatedSymbolEntry> isolated,
        CancellationToken ct)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (isolated.Count >= maxResults) break;
            ct.ThrowIfCancellationRequested();

            if (model.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol type) continue;
            if (IsSystemType(type) || ShouldSkip(type, includePublicTypes)) continue;
            if (HasSolutionTypeReferences(type, solutionTypeNames)) continue;

            var refs = await SymbolFinder.FindReferencesAsync(type, solution, ct);
            if (refs.Sum(r => r.Locations.Count()) > 0) continue;

            var loc = SymbolResolver.GetLocation(type);
            isolated.Add(new IsolatedSymbolEntry(type.ToDisplayString(), type.TypeKind.ToString(), loc.FilePath, loc.Line, project.Name));
        }
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

    private static bool HasSolutionTypeReferences(INamedTypeSymbol type, HashSet<string> solutionTypes) =>
        TypeStructureHelper.CollectStructuralTypeRefs(type)
            .Any(t => solutionTypes.Contains(t.ToDisplayString()));

    private static bool ShouldSkip(INamedTypeSymbol type, bool includePublicTypes)
    {
        if (type.IsImplicitlyDeclared || type.IsAnonymousType) return true;
        if (type.TypeKind is TypeKind.Delegate or TypeKind.Enum) return true;
        if (!includePublicTypes && type.DeclaredAccessibility == Accessibility.Public) return true;

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
