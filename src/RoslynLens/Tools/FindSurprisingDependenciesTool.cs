using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class FindSurprisingDependenciesTool
{
    [McpServerTool(Name = "find_surprising_dependencies")]
    [Description("Detects semantically unexpected cross-namespace dependencies: peripheral namespaces reaching hub namespaces, cross-assembly couplings, and structurally distant connections. Scored by surprise factors; returns the most unexpected edges first.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional project name filter (substring match)")] string? projectFilter = null,
        [Description("Maximum number of surprising dependencies to return (default 20)")] int maxResults = 20,
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

        var (edges, nsToProject) = await BuildEdgeGraphAsync(workspace, projects, ct);

        if (edges.Count == 0)
            return Json.Serialize(new SurprisingDependenciesResult([], 0));

        var result = ScoreAndRank(edges, nsToProject, maxResults);
        return WorkspaceManager.SerializeWithMultiSolutionHint(result);
    }

    private sealed class EdgeData
    {
        public int RefCount { get; set; }
        public string? FromProject { get; set; }
        public string? ToProject { get; set; }
    }

    private sealed class EdgeKeyComparer : IEqualityComparer<(string From, string To)>
    {
        public static readonly EdgeKeyComparer Instance = new();
        public bool Equals((string From, string To) x, (string From, string To) y) =>
            string.Equals(x.From, y.From, StringComparison.Ordinal) &&
            string.Equals(x.To, y.To, StringComparison.Ordinal);
        public int GetHashCode((string From, string To) obj) =>
            HashCode.Combine(obj.From.GetHashCode(StringComparison.Ordinal), obj.To.GetHashCode(StringComparison.Ordinal));
    }

    private static async Task<(Dictionary<(string, string), EdgeData> Edges, Dictionary<string, string> NsToProject)>
        BuildEdgeGraphAsync(WorkspaceManager workspace, List<Project> projects, CancellationToken ct)
    {
        var edges = new Dictionary<(string From, string To), EdgeData>(EdgeKeyComparer.Instance);
        var nsToProject = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            await ScanCompilationAsync(compilation, project.Name, edges, nsToProject, ct);
        }

        return (edges, nsToProject);
    }

    private static async Task ScanCompilationAsync(
        Compilation compilation,
        string projectName,
        Dictionary<(string, string), EdgeData> edges,
        Dictionary<string, string> nsToProject,
        CancellationToken ct)
    {
        await foreach (var (root, model) in TypeStructureHelper.GetTreeRootsAsync(compilation, ct))
            ScanTreeForEdges(root, model, projectName, edges, nsToProject, ct);
    }

    private static void ScanTreeForEdges(
        SyntaxNode root,
        SemanticModel model,
        string projectName,
        Dictionary<(string, string), EdgeData> edges,
        Dictionary<string, string> nsToProject,
        CancellationToken ct)
    {
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(typeDecl, ct) is not INamedTypeSymbol type) continue;

            var fromNs = type.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(fromNs) || TypeStructureHelper.IsSystemNamespace(fromNs)) continue;

            nsToProject.TryAdd(fromNs, projectName);
            CollectOutgoingEdges(type, fromNs, projectName, edges, nsToProject, ct);
        }
    }

    private static void CollectOutgoingEdges(
        INamedTypeSymbol type,
        string fromNs,
        string fromProject,
        Dictionary<(string, string), EdgeData> edges,
        Dictionary<string, string> nsToProject,
        CancellationToken ct)
    {
        foreach (var targetType in TypeStructureHelper.CollectStructuralTypeRefs(type, ct))
        {
            var toNs = targetType.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(toNs) || TypeStructureHelper.IsSystemNamespace(toNs) || toNs == fromNs) continue;

            var toProject = targetType.ContainingAssembly?.Name ?? toNs;
            nsToProject.TryAdd(toNs, toProject);

            var key = (fromNs, toNs);
            if (!edges.TryGetValue(key, out var data))
            {
                data = new EdgeData { FromProject = fromProject, ToProject = toProject };
                edges[key] = data;
            }
            data.RefCount++;
        }
    }

    private static SurprisingDependenciesResult ScoreAndRank(
        Dictionary<(string From, string To), EdgeData> edges,
        Dictionary<string, string> nsToProject,
        int maxResults)
    {
        var outDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (key, _) in edges)
        {
            outDegree[key.From] = outDegree.GetValueOrDefault(key.From) + 1;
            inDegree[key.To] = inDegree.GetValueOrDefault(key.To) + 1;
        }

        double meanInDegree = inDegree.Count > 0 ? inDegree.Values.Average() : 0;
        double stdInDegree = inDegree.Count > 0
            ? Math.Sqrt(inDegree.Values.Average(x => Math.Pow(x - meanInDegree, 2)))
            : 0;
        double hubThreshold = meanInDegree + stdInDegree;

        double medianOutDegree = outDegree.Count > 0
            ? outDegree.Values.OrderBy(x => x).ElementAt(outDegree.Count / 2)
            : 0;

        var scored = new List<(int Score, List<string> Reasons, string From, string To, int RefCount)>();

        foreach (var (key, data) in edges)
        {
            var (score, reasons) = ComputeSurpriseScore(key, outDegree, inDegree, nsToProject, hubThreshold, medianOutDegree);
            if (score > 0)
                scored.Add((score, reasons, key.From, key.To, data.RefCount));
        }

        var top = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.RefCount)
            .Take(maxResults)
            .Select(x => new SurprisingDependency(x.From, x.To, x.Score, x.Reasons, x.RefCount))
            .ToList();

        return new SurprisingDependenciesResult(top, top.Count);
    }

    private static (int Score, List<string> Reasons) ComputeSurpriseScore(
        (string From, string To) key,
        Dictionary<string, int> outDegree,
        Dictionary<string, int> inDegree,
        Dictionary<string, string> nsToProject,
        double hubThreshold,
        double medianOutDegree)
    {
        var reasons = new List<string>();
        var score = 0;

        var fromProj = nsToProject.GetValueOrDefault(key.From, "");
        var toProj = nsToProject.GetValueOrDefault(key.To, "");
        if (!string.Equals(fromProj, toProj, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(fromProj) && !string.IsNullOrEmpty(toProj))
        {
            score += 2;
            reasons.Add($"cross-assembly ({fromProj} → {toProj})");
        }

        var srcOut = outDegree.GetValueOrDefault(key.From);
        if (srcOut > 0 && srcOut <= medianOutDegree / 2.0)
        {
            score += 2;
            reasons.Add($"peripheral source ({srcOut} outgoing deps)");
        }

        var tgtIn = inDegree.GetValueOrDefault(key.To);
        if (tgtIn > hubThreshold)
        {
            score += 1;
            reasons.Add($"hub target ({tgtIn} incoming deps)");
        }

        var distance = ComputeNamespaceDistance(key.From, key.To);
        if (distance >= 3)
        {
            score += 1;
            reasons.Add($"distant namespaces (depth diff {distance})");
        }

        return (score, reasons);
    }

    private static int ComputeNamespaceDistance(string nsA, string nsB)
    {
        var partsA = nsA.Split('.');
        var partsB = nsB.Split('.');
        var commonLen = 0;
        var minLen = Math.Min(partsA.Length, partsB.Length);

        for (var i = 0; i < minLen; i++)
        {
            if (string.Equals(partsA[i], partsB[i], StringComparison.Ordinal))
                commonLen++;
            else
                break;
        }

        return (partsA.Length - commonLen) + (partsB.Length - commonLen);
    }

}
