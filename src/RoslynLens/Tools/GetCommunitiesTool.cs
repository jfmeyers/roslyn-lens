using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetCommunitiesTool
{
    [McpServerTool(Name = "get_communities")]
    [Description("Partitions solution namespaces into cohesive communities via label-propagation over the type-reference graph. Returns each community with a cohesion score (0–1) and common namespace prefix.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (substring match)")] string? projectFilter = null,
        [Description("Maximum label-propagation iterations (default 20)")] int maxIterations = 20,
        [Description("Maximum number of communities to return (default 30)")] int maxResults = 30,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var projects = solution.Projects
            .Where(p => projectFilter is null || p.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase));

        var nsEdges = await BuildNamespaceGraphAsync(workspace, projects, ct);

        if (nsEdges.Count == 0)
            return Json.Serialize(new CommunitiesResult([], 0, 0));

        var communities = RunLabelPropagation(nsEdges, maxIterations, ct);
        var entries = BuildCommunityEntries(communities, nsEdges, maxResults);

        var result = new CommunitiesResult(entries, nsEdges.Count, entries.Count);
        return WorkspaceManager.SerializeWithMultiSolutionHint(result);
    }

    private static async Task<Dictionary<string, Dictionary<string, int>>> BuildNamespaceGraphAsync(
        WorkspaceManager workspace,
        IEnumerable<Project> projects,
        CancellationToken ct)
    {
        var nsEdges = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            await ScanCompilationAsync(compilation, nsEdges, ct);
        }

        return nsEdges;
    }

    private static async Task ScanCompilationAsync(
        Compilation compilation,
        Dictionary<string, Dictionary<string, int>> nsEdges,
        CancellationToken ct)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            ct.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync(ct);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                RegisterTypeEdges(typeDecl, model, nsEdges, ct);
        }
    }

    private static void RegisterTypeEdges(
        TypeDeclarationSyntax typeDecl,
        SemanticModel model,
        Dictionary<string, Dictionary<string, int>> nsEdges,
        CancellationToken ct)
    {
        if (model.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol type) return;

        var sourceNs = type.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(sourceNs) || TypeStructureHelper.IsSystemNamespace(sourceNs)) return;

        nsEdges.TryAdd(sourceNs, new Dictionary<string, int>(StringComparer.Ordinal));

        foreach (var targetType in TypeStructureHelper.CollectStructuralTypeRefs(type, ct))
        {
            var targetNs = targetType.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(targetNs) || TypeStructureHelper.IsSystemNamespace(targetNs) || targetNs == sourceNs)
                continue;

            AddEdge(nsEdges, sourceNs, targetNs);
            AddEdge(nsEdges, targetNs, sourceNs);
        }
    }

    private static void AddEdge(
        Dictionary<string, Dictionary<string, int>> graph,
        string from, string to)
    {
        if (!graph.TryGetValue(from, out var neighbors))
        {
            neighbors = new Dictionary<string, int>(StringComparer.Ordinal);
            graph[from] = neighbors;
        }
        neighbors[to] = neighbors.GetValueOrDefault(to) + 1;
    }

    private static Dictionary<string, string> RunLabelPropagation(
        Dictionary<string, Dictionary<string, int>> graph,
        int maxIterations,
        CancellationToken ct)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ns in graph.Keys)
            labels[ns] = ns;

        for (var iter = 0; iter < maxIterations; iter++)
        {
            ct.ThrowIfCancellationRequested();
            var changed = false;

            foreach (var ns in graph.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var neighbors = graph[ns];
                if (neighbors.Count == 0) continue;

                var labelWeights = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (neighbor, weight) in neighbors)
                {
                    var label = labels.TryGetValue(neighbor, out var l) ? l : neighbor;
                    labelWeights[label] = labelWeights.GetValueOrDefault(label) + weight;
                }

                var bestLabel = labelWeights.MaxBy(kv => kv.Value).Key;
                if (bestLabel != labels[ns])
                {
                    labels[ns] = bestLabel;
                    changed = true;
                }
            }

            if (!changed) break;
        }

        return labels;
    }

    private static List<CommunityEntry> BuildCommunityEntries(
        Dictionary<string, string> labels,
        Dictionary<string, Dictionary<string, int>> graph,
        int maxResults)
    {
        var groups = labels
            .GroupBy(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Take(maxResults);

        var entries = new List<CommunityEntry>();

        foreach (var group in groups)
        {
            var members = group.ToHashSet(StringComparer.Ordinal);

            var memberList = members
                .Select(ns =>
                {
                    var degree = graph.TryGetValue(ns, out var neighbors) ? neighbors.Count : 0;
                    return new CommunityMember(ns, degree);
                })
                .OrderByDescending(m => m.Degree)
                .ToList();

            entries.Add(new CommunityEntry(
                group.Key, memberList,
                ComputeCohesion(members, graph),
                FindCommonPrefix(members)));
        }

        return entries;
    }

    private static float ComputeCohesion(
        HashSet<string> members,
        Dictionary<string, Dictionary<string, int>> graph)
    {
        if (members.Count <= 1) return 1.0f;

        var intraEdges = 0;
        var totalEdges = 0;

        foreach (var ns in members)
        {
            if (!graph.TryGetValue(ns, out var neighbors)) continue;
            foreach (var (neighbor, _) in neighbors)
            {
                totalEdges++;
                if (members.Contains(neighbor))
                    intraEdges++;
            }
        }

        return totalEdges == 0 ? 0f : (float)intraEdges / totalEdges;
    }

    private static string? FindCommonPrefix(IEnumerable<string> namespaces)
    {
        var parts = namespaces.Select(ns => ns.Split('.')).ToList();
        if (parts.Count == 0) return null;

        var prefix = new List<string>();
        var minLen = parts.Min(p => p.Length);

        for (var i = 0; i < minLen; i++)
        {
            var segment = parts[0][i];
            if (parts.All(p => string.Equals(p[i], segment, StringComparison.Ordinal)))
                prefix.Add(segment);
            else
                break;
        }

        return prefix.Count > 0 ? string.Join(".", prefix) : null;
    }

}
