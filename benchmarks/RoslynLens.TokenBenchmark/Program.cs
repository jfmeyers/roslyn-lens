using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.ML.Tokenizers;
using RoslynLens;
using RoslynLens.TokenBenchmark;

// Measures how many tokens RoslynLens saves versus reading source directly.
//
// For every uniquely-named type declared in source, it compares:
//   baseline = the full source text of the type declaration(s)  ("re-read the file")
//   lens     = the JSON returned by the real get_public_api tool ("navigate by structure")
//
// Both sides are counted with the same GPT-4o (o200k_base) tokenizer, whose vocabulary
// is embedded in the referenced data package — so the benchmark is fully offline and
// reproducible. Results are written to docs/BENCHMARKS.md and printed to stderr.

// MSBuildLocator MUST run before any Roslyn type is loaded.
MSBuildLocator.RegisterDefaults();

var ct = CancellationToken.None;

var solutionPath = SolutionDiscovery.FindSolutionPath(args);
if (solutionPath is null)
{
    await Console.Error.WriteLineAsync("No .sln/.slnx found. Pass a solution path as the first argument.");
    return 1;
}

await Console.Error.WriteLineAsync($"Loading {solutionPath} ...");
using var workspace = new WorkspaceManager(RoslynLensConfig.FromEnvironment());
await workspace.LoadSolutionAsync(solutionPath, ct);

var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
int CountTokens(string text) => tokenizer.CountTokens(text);

// Collect every source-declared named type across all projects.
var compilations = await workspace.GetAllCompilationsAsync(ct);
var allTypes = new List<INamedTypeSymbol>();
foreach (var (_, compilation) in compilations)
    CollectTypes(compilation.Assembly.GlobalNamespace, allTypes);

// Count top-level types per source file so we can restrict the comparison to files that
// declare exactly one type — the "one class per file" case where reading the file and
// reading the type are the same thing, keeping baseline vs lens a clean 1:1 measurement.
var topLevelPerFile = allTypes
    .Where(t => t.ContainingType is null)
    .SelectMany(t => t.DeclaringSyntaxReferences.Select(r => r.SyntaxTree.FilePath))
    .Where(p => !string.IsNullOrEmpty(p))
    .GroupBy(p => p)
    .ToDictionary(g => g.Key, g => g.Count());

// Restrict to types whose simple name is unique across the solution — this keeps the
// name-based get_public_api lookup unambiguous, so baseline and lens describe the same type.
var candidates = allTypes
    .Where(t => t.ContainingType is null)
    .GroupBy(t => t.Name)
    .Where(g => g.Count() == 1)
    .Select(g => g.First())
    .ToList();

var rows = new List<Row>();
var skipped = 0;
foreach (var type in candidates)
{
    var files = type.DeclaringSyntaxReferences
        .Select(r => r.SyntaxTree.FilePath)
        .Where(p => !string.IsNullOrEmpty(p))
        .Distinct()
        .ToList();

    // Only single-file, single-type declarations — the honest "replace one file read" case.
    if (files.Count != 1 || topLevelPerFile.GetValueOrDefault(files[0]) != 1)
    {
        skipped++;
        continue;
    }

    var sourceText = (await type.DeclaringSyntaxReferences[0].SyntaxTree.GetTextAsync(ct)).ToString();
    if (sourceText.Length == 0)
    {
        skipped++;
        continue;
    }

    var lensJson = await GetPublicApiTool.ExecuteAsync(workspace, type.Name, ct);

    // Skip types the tool could not resolve to a non-empty public API.
    if (GetTotal(lensJson) <= 0)
    {
        skipped++;
        continue;
    }

    rows.Add(new Row(
        type.Name,
        CountTokens(sourceText),
        CountTokens(lensJson)));
}

if (rows.Count == 0)
{
    await Console.Error.WriteLineAsync("No comparable types found — nothing to report.");
    return 1;
}

var pooledBaseline = rows.Sum(r => (long)r.BaselineTokens);
var pooledLens = rows.Sum(r => (long)r.LensTokens);
var pooledReduction = 1.0 - (double)pooledLens / pooledBaseline;

var reductions = rows.Select(r => 1.0 - (double)r.LensTokens / r.BaselineTokens).OrderBy(x => x).ToList();
var medianReduction = Median(reductions);

var solutionName = Path.GetFileName(solutionPath);
var report = BuildReport(solutionName, rows, medianReduction, pooledReduction, pooledBaseline, pooledLens, skipped);

var solutionDir = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
var docsDir = Path.Combine(solutionDir, "docs");
Directory.CreateDirectory(docsDir);
var outputPath = Path.Combine(docsDir, "BENCHMARKS.md");
await File.WriteAllTextAsync(outputPath, report, ct);

await Console.Error.WriteLineAsync(
    $"""

    Types compared : {rows.Count} (skipped {skipped})
    Median reduction : {medianReduction:P1}
    Pooled reduction : {pooledReduction:P1} ({pooledBaseline:N0} -> {pooledLens:N0} tokens)
    Report written   : {outputPath}
    """);

return 0;

static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> sink)
{
    foreach (var type in ns.GetTypeMembers())
    {
        if (type.Locations.Any(l => l.IsInSource) && type.DeclaringSyntaxReferences.Length > 0)
            sink.Add(type);
        foreach (var nested in type.GetTypeMembers())
            sink.Add(nested);
    }

    foreach (var child in ns.GetNamespaceMembers())
        CollectTypes(child, sink);
}

static int GetTotal(string lensJson)
{
    try
    {
        using var doc = JsonDocument.Parse(lensJson);
        return doc.RootElement.TryGetProperty("Total", out var total) ? total.GetInt32() : 0;
    }
    catch (JsonException)
    {
        return 0;
    }
}

static double Median(List<double> sorted)
{
    if (sorted.Count == 0) return 0;
    var mid = sorted.Count / 2;
    return sorted.Count % 2 == 1
        ? sorted[mid]
        : (sorted[mid - 1] + sorted[mid]) / 2.0;
}

static string BuildReport(
    string solutionName,
    List<Row> rows,
    double medianReduction,
    double pooledReduction,
    long pooledBaseline,
    long pooledLens,
    int skipped)
{
    var ci = CultureInfo.InvariantCulture;
    var sb = new StringBuilder();
    sb.AppendLine("# Token benchmark");
    sb.AppendLine();
    // Wrapped at <= 80 columns to satisfy markdownlint MD013.
    sb.AppendLine("Generated by `benchmarks/RoslynLens.TokenBenchmark`. For every");
    sb.AppendLine("single-type source file, it compares the full file (what an agent pays to");
    sb.AppendLine("read when it opens the file) against the JSON returned by the real");
    sb.AppendLine("`get_public_api` tool. Both sides are counted with the GPT-4o");
    sb.AppendLine("(`o200k_base`) tokenizer whose vocabulary ships embedded in the package,");
    sb.AppendLine("so the run is offline and reproducible.");
    sb.AppendLine();
    sb.AppendLine($"- **Solution**: `{solutionName}`");
    sb.AppendLine($"- **Types compared**: {rows.Count.ToString("N0", ci)} (skipped {skipped.ToString("N0", ci)} unresolved/empty)");
    sb.AppendLine($"- **Median reduction**: {medianReduction.ToString("P1", ci)}");
    sb.AppendLine($"- **Pooled reduction (size-weighted)**: {pooledReduction.ToString("P1", ci)}");
    sb.AppendLine($"- **Total tokens**: {pooledBaseline.ToString("N0", ci)} source -> {pooledLens.ToString("N0", ci)} lens");
    sb.AppendLine();
    sb.AppendLine("## Largest 20 types by source size");
    sb.AppendLine();
    sb.AppendLine("| Type | Source tokens | Lens tokens | Reduction |");
    sb.AppendLine("| ---- | ------------: | ----------: | --------: |");
    foreach (var r in rows.OrderByDescending(r => r.BaselineTokens).Take(20))
    {
        var reduction = 1.0 - (double)r.LensTokens / r.BaselineTokens;
        sb.AppendLine(
            $"| `{r.Name}` | {r.BaselineTokens.ToString("N0", ci)} | " +
            $"{r.LensTokens.ToString("N0", ci)} | {reduction.ToString("P1", ci)} |");
    }
    return sb.ToString();
}

namespace RoslynLens.TokenBenchmark
{
    internal readonly record struct Row(string Name, int BaselineTokens, int LensTokens);
}
