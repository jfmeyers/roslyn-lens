using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace RoslynLens.Tests.Tools;

[Collection("StaticState")]
public class ListSolutionsToolTests : IDisposable
{
    private readonly WorkspaceManager _workspace;

    public ListSolutionsToolTests()
    {
        var config = new RoslynLensConfig(30, 100, 50, LogLevel.Warning);
        _workspace = new WorkspaceManager(config);
    }

    [Fact]
    public async Task Returns_All_Discovered_Solutions_With_IsActive_Flag()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;
        var originalPath = WorkspaceInitializer.SolutionPath;

        try
        {
            var pathA = Path.Combine(Path.GetTempPath(), "repos", "SolutionA.slnx");
            var pathB = Path.Combine(Path.GetTempPath(), "repos", "SolutionB.slnx");

            WorkspaceInitializer.DiscoveredSolutions = [pathA, pathB];
            WorkspaceInitializer.SolutionPath = pathA;

            var result = await ListSolutionsTool.ExecuteAsync(_workspace, TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            root.GetProperty("Solutions").GetArrayLength().ShouldBe(2);

            var first = root.GetProperty("Solutions")[0];
            first.GetProperty("Name").GetString().ShouldBe("SolutionA.slnx");
            first.GetProperty("IsActive").GetBoolean().ShouldBeTrue();

            var second = root.GetProperty("Solutions")[1];
            second.GetProperty("Name").GetString().ShouldBe("SolutionB.slnx");
            second.GetProperty("IsActive").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
            WorkspaceInitializer.SolutionPath = originalPath;
        }
    }

    [Fact]
    public async Task Returns_Empty_When_No_Solutions_Discovered()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = [];

            var result = await ListSolutionsTool.ExecuteAsync(_workspace, TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(result);
            doc.RootElement.GetProperty("Solutions").GetArrayLength().ShouldBe(0);
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    [Fact]
    public async Task Includes_Multi_Solution_Hint_When_Multiple_Discovered()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;
        var originalPath = WorkspaceInitializer.SolutionPath;

        try
        {
            var pathA = Path.Combine(Path.GetTempPath(), "repos", "A.slnx");
            var pathB = Path.Combine(Path.GetTempPath(), "repos", "B.slnx");
            var pathC = Path.Combine(Path.GetTempPath(), "repos", "C.slnx");

            WorkspaceInitializer.DiscoveredSolutions = [pathA, pathB, pathC];
            WorkspaceInitializer.SolutionPath = pathA;

            var result = await ListSolutionsTool.ExecuteAsync(_workspace, TestContext.Current.CancellationToken);

            using var doc = JsonDocument.Parse(result);
            var hint = doc.RootElement.GetProperty("Hint").GetString();
            hint.ShouldNotBeNullOrEmpty();
            hint.ShouldContain("3 solutions discovered");
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
            WorkspaceInitializer.SolutionPath = originalPath;
        }
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
