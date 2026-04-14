using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class SwitchSolutionTool
{
    [McpServerTool(Name = "switch_solution")]
    [Description("Switch to a different solution from the discovered list. Use list_solutions to see available options.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Full path to the solution file (must be one of the discovered solutions)")] string solutionPath,
        CancellationToken ct = default)
    {
        var discovered = WorkspaceInitializer.DiscoveredSolutions;

        if (!discovered.Contains(solutionPath, StringComparer.OrdinalIgnoreCase))
        {
            return Json.Serialize(new
            {
                error = "Path not in discovered solutions list. Use list_solutions to see available options.",
                discovered = discovered
            });
        }

        try
        {
            await workspace.ReloadSolutionAsync(solutionPath, ct);
            WorkspaceInitializer.SolutionPath = solutionPath;

            return Json.Serialize(new
            {
                status = "switched",
                solution = Path.GetFileName(solutionPath),
                projectCount = workspace.ProjectCount
            });
        }
        catch (Exception ex)
        {
            return Json.Serialize(new
            {
                error = $"Failed to switch solution: {ex.Message}",
                state = workspace.State.ToString()
            });
        }
    }
}
