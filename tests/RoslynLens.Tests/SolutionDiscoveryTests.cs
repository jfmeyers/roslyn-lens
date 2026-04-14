using Shouldly;

namespace RoslynLens.Tests;

public class SolutionDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    #region Test plumbing

    /// <summary>
    /// Initializes a new instance of the SolutionDiscoveryTests class and prepares the test environment.
    /// </summary>
    /// <remarks>Creates the temporary directory required for test execution. This constructor is intended for
    /// use in test setup scenarios.</remarks>
    public SolutionDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Console.WriteLine("[SolutionDiscoveryTests] Created temp directory: {0}", _tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }

        GC.SuppressFinalize(this);
    }

    private string CreateSubDir(string name)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateSolutionFile(string relativePath, string? content = null)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        
        Directory.CreateDirectory(dir);

        if (string.IsNullOrEmpty(content))
        {
            content = Path.GetExtension(fullPath) switch {
                ".slnx" => "<Solution />",
                ".sln" => """
                       
                       Microsoft Visual Studio Solution File, Format Version 12.00
                       # Visual Studio Version 17
                       """,
                _ => ""
            };
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    #endregion

    [Fact]
    public void FindSolutionPath_With_Explicit_Arg_Returns_Path()
    {
        var slnPath = CreateSolutionFile("Test.slnx");

        var result = SolutionDiscovery.FindSolutionPath(["--solution", slnPath]);
        result.ShouldNotBeNull();
        result.ShouldEndWith("Test.slnx");
    }

    [Fact]
    public void BfsDiscovery_Finds_Slnx_In_Current_Dir()
    {
        CreateSolutionFile("MyApp.slnx");

        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldNotBeNull();
        result.ShouldEndWith("MyApp.slnx");
    }

    [Fact]
    public void BfsDiscovery_Returns_Null_When_No_Solution()
    {
        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldBeNull();
    }

    [Fact]
    public void BfsDiscovery_Finds_Sln_In_Subdirectory()
    {
        CreateSolutionFile(Path.Combine("src", "App.sln"));

        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldNotBeNull();
        result.ShouldEndWith("App.sln");
    }

    [Fact]
    public void BfsDiscovery_Prefers_Shallower_Depth()
    {
        CreateSolutionFile("Root.sln");
        CreateSolutionFile(Path.Combine("src", "Nested.sln"));

        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldNotBeNull();
        result.ShouldEndWith("Root.sln");
    }

    [Fact]
    public void BfsDiscovery_Skips_Known_Directories()
    {
        CreateSolutionFile(Path.Combine("bin", "Hidden.sln"));

        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldBeNull();
    }

    [Fact]
    public void BfsDiscovery_Returns_Null_Beyond_MaxBfsDepth()
    {
        // MaxBfsDepth is 3, so depth 4 should be ignored
        CreateSolutionFile(Path.Combine("a", "b", "c", "d", "TooDeep.sln"));

        var result = SolutionDiscovery.BfsDiscovery(_tempDir);
        result.ShouldBeNull();
    }

    [Fact]
    public void BfsDiscoverAll_ReturnsAllSolutions_InAllDirectories()
    {
        CreateSolutionFile("SolutionA.slnx");
        CreateSolutionFile("A-Solution.slnx");
        CreateSolutionFile(Path.Combine("src", "Nested.sln"));

        var results = SolutionDiscovery.BfsDiscoverAll(_tempDir);
        results.Count.ShouldBe(3);
        results.ShouldContain(p => p.EndsWith("SolutionA.slnx"));
        results.ShouldContain(p => p.EndsWith("A-Solution.slnx"));
        results.ShouldContain(p => p.EndsWith("Nested.sln"));
    }

    [Fact]
    public void BfsDiscoverAll_Orders_By_Depth_Then_Alphabetically()
    {
        CreateSolutionFile("Root.sln");
        CreateSolutionFile(Path.Combine("src", "Nested.sln"));
        CreateSolutionFile(Path.Combine("src", "Alpha.sln"));

        var results = SolutionDiscovery.BfsDiscoverAll(_tempDir);
        results.Count.ShouldBe(3);
        // Depth 0 first, then depth 1 alphabetically
        results[0].ShouldEndWith("Root.sln");
        results[1].ShouldEndWith("Alpha.sln");
        results[2].ShouldEndWith("Nested.sln");
    }

    [Fact]
    public void BfsDiscoverAll_Returns_Empty_When_No_Solutions()
    {
        var results = SolutionDiscovery.BfsDiscoverAll(_tempDir);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void BfsDiscoverAll_SkipsKnownDirectories()
    {
        // SolutionDiscovery has a list of ignored directories (e.g. node_modules, obj, bin, packages), so solutions in those should be skipped
        CreateSolutionFile(Path.Combine("node_modules", "skip_me.sln"));
        CreateSolutionFile(Path.Combine("packages", "Nupkg", "whatever.slnx"));
        CreateSolutionFile(Path.Combine("obj", "skip_me_too.slnx"));
        CreateSolutionFile(Path.Combine("src", "Visible.sln"));

        var results = SolutionDiscovery.BfsDiscoverAll(_tempDir);
        results.Count.ShouldBe(1);
        results[0].ShouldEndWith("Visible.sln");
    }

    [Fact]
    public void BfsDiscoverAll_Ignores_Solutions_Beyond_MaxBfsDepth()
    {
        // Sanity check to ensure the constant is what we expect
        SolutionDiscovery.MaxBfsDepth.ShouldBe(3);

        // MaxBfsDepth is 3, so depth 4 should be ignored
        CreateSolutionFile("Root.sln");
        CreateSolutionFile(Path.Combine("a", "b", "c", "d", "TooDeep.slnx"));

        var results = SolutionDiscovery.BfsDiscoverAll(_tempDir);
        results.Count.ShouldBe(1);
        results[0].ShouldEndWith("Root.sln");
    }

    [Fact]
    public void FindSolutionPath_Without_Explicit_Arg_Uses_Bfs()
    {
        var cwd = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            var result = SolutionDiscovery.FindSolutionPath([]);
            result.ShouldBeNull();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
        }
    }
}
