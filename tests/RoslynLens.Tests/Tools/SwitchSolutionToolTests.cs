using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace RoslynLens.Tests.Tools;

[Collection("StaticState")]
public class SwitchSolutionToolTests : IDisposable
{
    private readonly WorkspaceManager _workspace;

    public SwitchSolutionToolTests()
    {
        var config = new RoslynLensConfig(30, 100, 50, LogLevel.Warning);
        _workspace = new WorkspaceManager(config);
    }

    [Fact]
    public async Task Rejects_Path_Not_In_Discovered_List()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            var pathA = Path.Combine(Path.GetTempPath(), "repos", "SolutionA.slnx");
            var pathB = Path.Combine(Path.GetTempPath(), "repos", "SolutionB.slnx");
            var unknown = Path.Combine(Path.GetTempPath(), "repos", "Unknown.slnx");

            WorkspaceInitializer.DiscoveredSolutions = [pathA, pathB];

            var result = await SwitchSolutionTool.ExecuteAsync(
                _workspace,
                unknown,
                TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(result);
            var error = doc.RootElement.GetProperty("error").GetString();
            error.ShouldNotBeNullOrEmpty();
            error.ShouldContain("not in discovered");
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    [Fact]
    public async Task Rejects_When_No_Solutions_Discovered()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = [];

            var result = await SwitchSolutionTool.ExecuteAsync(
                _workspace,
                Path.Combine(Path.GetTempPath(), "repos", "Anything.slnx"),
                TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(result);
            var error = doc.RootElement.GetProperty("error").GetString();
            error.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    public void Dispose()
    {
        _workspace.Dispose();
        GC.SuppressFinalize(this);
    }
}
