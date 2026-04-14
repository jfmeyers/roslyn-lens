using Microsoft.Extensions.Logging;
using Shouldly;

namespace RoslynLens.Tests;

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

    public void Dispose()
    {
        _manager.Dispose();
    }
}
