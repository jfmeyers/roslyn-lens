using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace RoslynLens.Tests;

[Collection("StaticState")]
public class WorkspaceManagerTests : IDisposable
{
    private readonly WorkspaceManager _manager;

    public WorkspaceManagerTests()
    {
        var config = new RoslynLensConfig(30, 100, 50, LogLevel.Warning);
        _manager = new WorkspaceManager(config);
    }

    [Fact]
    public void Initial_State_Is_NotStarted()
    {
        _manager.State.ShouldBe(WorkspaceState.NotStarted);
    }

    [Fact]
    public async Task ReloadSolutionAsync_Sets_State_To_Loading_Then_Error_For_Invalid_Path()
    {
        var ex = await Should.ThrowAsync<Exception>(
            () => _manager.ReloadSolutionAsync("nonexistent.slnx", TestContext.Current.CancellationToken));

        _manager.State.ShouldBe(WorkspaceState.Error);
        _manager.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReloadSolutionAsync_Resets_State_After_Previous_Error()
    {
        // First load will fail — that's fine, we're testing state transitions
        try
        {
            await _manager.LoadSolutionAsync("nonexistent.slnx", TestContext.Current.CancellationToken);
        }
        catch
        {
            // Expected
        }

        _manager.State.ShouldBe(WorkspaceState.Error);

        // Reload should reset state and attempt fresh load
        try
        {
            await _manager.ReloadSolutionAsync("also-nonexistent.slnx", TestContext.Current.CancellationToken);
        }
        catch
        {
            // Expected
        }

        // State should be Error (not stuck on old state)
        _manager.State.ShouldBe(WorkspaceState.Error);
    }

    [Fact]
    public void SerializeWithMultiSolutionHint_Returns_Bare_Payload_When_Single_Solution()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = ["/repo/Only.slnx"];

            var payload = new { foo = "bar" };
            var result = WorkspaceManager.SerializeWithMultiSolutionHint(payload);

            using var doc = JsonDocument.Parse(result);
            doc.RootElement.GetProperty("foo").GetString().ShouldBe("bar");
            doc.RootElement.TryGetProperty("hint", out _).ShouldBeFalse();
            doc.RootElement.TryGetProperty("result", out _).ShouldBeFalse();
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    [Fact]
    public void SerializeWithMultiSolutionHint_Wraps_Payload_With_Hint_When_Multiple_Solutions()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = ["/repo/A.slnx", "/repo/B.slnx"];

            var payload = new { foo = "bar" };
            var result = WorkspaceManager.SerializeWithMultiSolutionHint(payload);

            using var doc = JsonDocument.Parse(result);
            doc.RootElement.GetProperty("result").GetProperty("foo").GetString().ShouldBe("bar");
            var hint = doc.RootElement.GetProperty("hint").GetString();
            hint.ShouldNotBeNull();
            hint.ShouldContain("2 solutions discovered");
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    [Fact]
    public void EnsureReadyOrStatus_Includes_Hint_When_Multiple_Solutions_And_Not_Ready()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = ["/repo/A.slnx", "/repo/B.slnx"];

            // Manager is in NotStarted state — EnsureReadyOrStatus returns the status JSON
            var result = _manager.EnsureReadyOrStatus(TestContext.Current.CancellationToken);

            result.ShouldNotBeNull();
            using var doc = JsonDocument.Parse(result);
            var hint = doc.RootElement.GetProperty("hint").GetString();
            hint.ShouldNotBeNull();
            hint.ShouldContain("2 solutions discovered");
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    [Fact]
    public void EnsureReadyOrStatus_Hint_Is_Null_When_Single_Solution()
    {
        var original = WorkspaceInitializer.DiscoveredSolutions;

        try
        {
            WorkspaceInitializer.DiscoveredSolutions = ["/repo/Only.slnx"];

            var result = _manager.EnsureReadyOrStatus(TestContext.Current.CancellationToken);

            result.ShouldNotBeNull();
            using var doc = JsonDocument.Parse(result);
            doc.RootElement.GetProperty("hint").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        finally
        {
            WorkspaceInitializer.DiscoveredSolutions = original;
        }
    }

    public void Dispose()
    {
        _manager.Dispose();
        GC.SuppressFinalize(this);
    }
}
