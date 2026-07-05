namespace RoslynLens;

/// <summary>
/// Discovers .sln/.slnx files using BFS from a starting directory.
/// Resolution order: explicit --solution arg > ROSLYN_LENS_SOLUTION env > working directory BFS.
/// </summary>
public static class SolutionDiscovery
{
    internal const string SolutionEnvVar = "ROSLYN_LENS_SOLUTION";

    internal static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "node_modules", "bin", "obj",
        "packages", "artifacts", "TestResults", ".claude", "nupkgs"
    };

    public const int MaxBfsDepth = 3;

    public static string? FindSolutionPath(string[] args)
    {
        var explicitPath = ParseExplicitPath(args);
        if (explicitPath is not null)
            return explicitPath;

        var envPath = ParseEnvPath();
        return envPath ?? BfsDiscovery(Directory.GetCurrentDirectory());
    }

    // Lets a host (e.g. the MCP bundle) point at a solution without passing CLI args.
    // An empty/blank value is treated as unset so it falls through to discovery.
    private static string? ParseEnvPath()
    {
        var value = Environment.GetEnvironmentVariable(SolutionEnvVar);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var path = value.Trim();

        if (File.Exists(path) && IsSolutionFile(path))
            return Path.GetFullPath(path);

        if (Directory.Exists(path))
            return BfsDiscovery(path);

        return null;
    }

    private static string? ParseExplicitPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--solution" or "-s")
            {
                var path = args[i + 1];

                if (File.Exists(path) && IsSolutionFile(path))
                    return Path.GetFullPath(path);

                if (Directory.Exists(path))
                    return BfsDiscovery(path);
            }
        }

        return null;
    }

    public static string? BfsDiscovery(string startDirectory)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((startDirectory, 0));

        string? bestMatch = null;
        var bestDepth = int.MaxValue;

        while (queue.Count > 0)
        {
            var (dirPath, depth) = queue.Dequeue();

            if (depth > MaxBfsDepth)
                continue;

            try
            {
                bestMatch = ScanDirectoryForSolution(dirPath, depth, bestMatch, ref bestDepth);

                if (bestMatch?.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) == true && depth == bestDepth)
                    return bestMatch;

                EnqueueSubDirectories(queue, dirPath, depth);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        return bestMatch;
    }

    public static IReadOnlyList<string> BfsDiscoverAll(string startDirectory)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((startDirectory, 0));

        var results = new List<(string FullPath, int Depth)>();

        while (queue.Count > 0)
        {
            var (dirPath, depth) = queue.Dequeue();

            if (depth > MaxBfsDepth)
                continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dirPath).Where(IsSolutionFile))
                {
                    results.Add((Path.GetFullPath(file), depth));
                }

                EnqueueSubDirectories(queue, dirPath, depth);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        return results
            .OrderBy(r => r.Depth)
            .ThenBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.FullPath)
            .ToList();
    }

    private static string? ScanDirectoryForSolution(string dirPath, int depth, string? currentBest, ref int bestDepth)
    {
        foreach (var file in Directory.EnumerateFiles(dirPath).Where(IsSolutionFile))
        {
            if (depth < bestDepth ||
                (depth == bestDepth && string.Compare(file, currentBest, StringComparison.OrdinalIgnoreCase) < 0))
            {
                currentBest = Path.GetFullPath(file);
                bestDepth = depth;
            }
        }

        return currentBest;
    }

    private static void EnqueueSubDirectories(Queue<(string Path, int Depth)> queue, string dirPath, int depth)
    {
        foreach (var subDir in Directory.EnumerateDirectories(dirPath))
        {
            var dirName = Path.GetFileName(subDir);
            if (!SkipDirectories.Contains(dirName))
                queue.Enqueue((subDir, depth + 1));
        }
    }

    private static bool IsSolutionFile(string path) =>
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
}
