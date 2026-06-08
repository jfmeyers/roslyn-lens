using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindGodNodesTool
{
    [McpServerTool(Name = "find_god_nodes")]
    [Description("Identifies types or methods with disproportionately high incoming reference counts (god nodes). Flags symbols whose reference count exceeds mean + threshold × stddev. These are coupling hotspots worth reviewing for decomposition.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S107:Methods should not have too many parameters", Justification = "MCP tool parameters are protocol-mandated")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (substring match)")] string? projectFilter = null,
        [Description("Symbol kind to analyze: 'types' or 'methods' (default 'types')")] string kind = "types",
        [Description("Standard deviation multiplier for god-node threshold (default 2.0)")] double threshold = 2.0,
        [Description("Minimum incoming references to be considered a candidate (default 5)")] int minRefs = 5,
        [Description("Maximum number of results to return (default 20)")] int maxResults = 20,
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

        var candidates = await CollectCandidatesAsync(workspace, solution, projects, kind, ct);

        if (candidates.Count == 0)
            return Json.Serialize(new GodNodesResult([], 0, 0, 0, 0));

        var refCounts = candidates.Select(c => (double)c.RefCount).ToList();
        var mean = refCounts.Average();
        var stdDev = Math.Sqrt(refCounts.Average(x => Math.Pow(x - mean, 2)));
        var cutoff = mean + threshold * stdDev;

        var godNodes = candidates
            .Where(c => c.RefCount >= minRefs && c.RefCount >= cutoff)
            .OrderByDescending(c => c.RefCount)
            .Take(maxResults)
            .Select(c => new GodNodeEntry(c.Name, c.Kind, c.RefCount, c.File, c.Line, c.Project))
            .ToList();

        var result = new GodNodesResult(godNodes, godNodes.Count, mean, stdDev, cutoff);
        return WorkspaceManager.SerializeWithMultiSolutionHint(result);
    }

    private sealed record Candidate(string Name, string Kind, int RefCount, string? File, int? Line, string? Project);

    private static async Task<List<Candidate>> CollectCandidatesAsync(
        WorkspaceManager workspace,
        Solution solution,
        List<Project> projects,
        string kind,
        CancellationToken ct)
    {
        var candidates = new List<Candidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

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

                foreach (var symbol in CollectSymbols(root, model, kind, ct))
                    await ProcessSymbolAsync(symbol, solution, project.Name, seen, candidates, ct);
            }
        }

        return candidates;
    }

    private static async Task ProcessSymbolAsync(
        ISymbol symbol,
        Solution solution,
        string projectName,
        HashSet<string> seen,
        List<Candidate> candidates,
        CancellationToken ct)
    {
        var key = symbol.ToDisplayString();
        if (!seen.Add(key)) return;
        if (IsSystemSymbol(symbol)) return;

        var refs = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
        var refCount = refs.Sum(r => r.Locations.Count());

        var loc = SymbolResolver.GetLocation(symbol);
        candidates.Add(new Candidate(key, symbol.Kind.ToString(), refCount, loc.FilePath, loc.Line, projectName));
    }

    private static IEnumerable<ISymbol> CollectSymbols(
        SyntaxNode root,
        SemanticModel model,
        string kind,
        CancellationToken ct)
    {
        IEnumerable<SyntaxNode> nodes = kind.ToLowerInvariant() switch
        {
            "methods" => root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
            _ => root.DescendantNodes().OfType<TypeDeclarationSyntax>()
        };

        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();
            var symbol = model.GetDeclaredSymbol(node, ct);
            if (symbol is not null)
                yield return symbol;
        }
    }

    private static bool IsSystemSymbol(ISymbol symbol)
    {
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal));
    }
}
