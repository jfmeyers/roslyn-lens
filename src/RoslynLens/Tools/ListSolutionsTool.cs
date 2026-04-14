using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynLens.Responses;

namespace RoslynLens;

[McpServerToolType]
public static class ListSolutionsTool
{
    [McpServerTool(Name = "list_solutions")]
    [Description("Lists all discovered solution files with their paths and which one is currently active.")]
    public static Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        CancellationToken ct = default)
    {
        var discovered = WorkspaceInitializer.DiscoveredSolutions;
        var activePath = WorkspaceInitializer.SolutionPath;

        var entries = discovered.Select(path => new SolutionEntry(
            path,
            Path.GetFileName(path),
            string.Equals(path, activePath, StringComparison.OrdinalIgnoreCase)
        )).ToList();

        var hint = WorkspaceManager.GetMultiSolutionHint();
        var result = new ListSolutionsResult(entries, hint);

        return Task.FromResult(Json.Serialize(result));
    }
}
