using System.Collections.Immutable;
using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class GetDiagnosticsTool
{
    [McpServerTool(Name = "get_diagnostics", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Returns compiler and analyzer diagnostics for a file, project, or the entire solution.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'file', 'project', or 'solution' (default 'solution')")] string scope = "solution",
        [Description("File or project path (required for 'file' and 'project' scopes)")] string? path = null,
        [Description("Minimum severity filter: 'error', 'warning', 'info', or 'hidden' (default 'warning')")] string severityFilter = "warning",
        [Description("Also run the bundled Roslynator analyzers (500+ rules). Slower; off by default.")] bool includeAnalyzers = false,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var solution = workspace.GetSolution();
        if (solution is null)
            return Json.Serialize(new { error = "No solution loaded" });

        var minSeverity = ParseSeverity(severityFilter);

        return scope.ToLowerInvariant() switch
        {
            "file" => await AnalyzeFileScopeAsync(workspace, solution, path, minSeverity, includeAnalyzers, ct),
            "project" => await AnalyzeProjectScopeAsync(workspace, solution, path, minSeverity, includeAnalyzers, ct),
            _ => await AnalyzeSolutionScopeAsync(workspace, solution, minSeverity, includeAnalyzers, ct)
        };
    }

    private static async Task<string> AnalyzeFileScopeAsync(
        WorkspaceManager workspace, Solution solution, string? path,
        DiagnosticSeverity minSeverity, bool includeAnalyzers, CancellationToken ct)
    {
        if (path is null)
            return Json.Serialize(new { error = "Path is required for file scope" });

        var diagnostics = new List<DiagnosticInfo>();
        var normalizedPath = path.Replace('\\', '/');

        foreach (var project in solution.Projects)
        {
            var doc = project.Documents
                .FirstOrDefault(d => d.FilePath?.Replace('\\', '/').EndsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) == true);

            if (doc is null) continue;

            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            var tree = await doc.GetSyntaxTreeAsync(ct);
            if (tree is null) continue;

            if (includeAnalyzers)
            {
                // Analyzers run per compilation; keep only diagnostics located in this file.
                var all = await RunWithAnalyzersAsync(compilation, ct);
                CollectDiagnostics(all.Where(d => d.Location.SourceTree == tree), minSeverity, diagnostics);
            }
            else
            {
                var model = compilation.GetSemanticModel(tree);
                CollectDiagnostics(model.GetDiagnostics(cancellationToken: ct), minSeverity, diagnostics);
            }
            break;
        }

        return SerializeResult(diagnostics, "file");
    }

    private static async Task<string> AnalyzeProjectScopeAsync(
        WorkspaceManager workspace, Solution solution, string? path,
        DiagnosticSeverity minSeverity, bool includeAnalyzers, CancellationToken ct)
    {
        if (path is null)
            return Json.Serialize(new { error = "Path is required for project scope" });

        var project = solution.Projects
            .FirstOrDefault(p => p.Name.Equals(path, StringComparison.OrdinalIgnoreCase)
                || (p.FilePath?.Replace('\\', '/').EndsWith(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) == true));

        if (project is null)
            return Json.Serialize(new { error = $"Project '{path}' not found" });

        var diagnostics = new List<DiagnosticInfo>();
        var compilation = await workspace.GetCompilationAsync(project, ct);
        if (compilation is not null)
            CollectDiagnostics(await GetDiagnosticsAsync(compilation, includeAnalyzers, ct), minSeverity, diagnostics);

        return SerializeResult(diagnostics, "project");
    }

    private static async Task<string> AnalyzeSolutionScopeAsync(
        WorkspaceManager workspace, Solution solution,
        DiagnosticSeverity minSeverity, bool includeAnalyzers, CancellationToken ct)
    {
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is not null)
                CollectDiagnostics(await GetDiagnosticsAsync(compilation, includeAnalyzers, ct), minSeverity, diagnostics);
        }

        return SerializeResult(diagnostics, "solution");
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Compilation compilation, bool includeAnalyzers, CancellationToken ct) =>
        includeAnalyzers
            ? await RunWithAnalyzersAsync(compilation, ct)
            : compilation.GetDiagnostics(ct);

    private static async Task<ImmutableArray<Diagnostic>> RunWithAnalyzersAsync(
        Compilation compilation, CancellationToken ct)
    {
        var analyzers = RoslynatorAnalyzers.Analyzers;
        if (analyzers.IsEmpty)
            return compilation.GetDiagnostics(ct);

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);

        // GetAllDiagnosticsAsync returns compiler diagnostics plus every analyzer's output.
        var all = await compilation.WithAnalyzers(analyzers, options).GetAllDiagnosticsAsync(ct);

        // Drop Roslynator's "fade out" companion diagnostics — they only tell an IDE which
        // spans to grey out and duplicate the real finding, so they are pure token noise.
        return all.Where(d => !d.Id.EndsWith("FadeOut", StringComparison.Ordinal)).ToImmutableArray();
    }

    private static string SerializeResult(List<DiagnosticInfo> diagnostics, string scope)
    {
        var result = new DiagnosticsResult(diagnostics, diagnostics.Count, scope);
        return scope == "solution"
            ? WorkspaceManager.SerializeWithMultiSolutionHint(result)
            : Json.Serialize(result);
    }

    private static void CollectDiagnostics(
        IEnumerable<Diagnostic> source,
        DiagnosticSeverity minSeverity,
        List<DiagnosticInfo> target)
    {
        foreach (var diag in source.Where(d => d.Severity >= minSeverity))
        {
            var lineSpan = diag.Location.GetMappedLineSpan();
            target.Add(new DiagnosticInfo(
                diag.Id,
                diag.Severity.ToString(),
                diag.GetMessage(),
                lineSpan.Path,
                lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null));
        }
    }

    private static DiagnosticSeverity ParseSeverity(string filter) =>
        filter.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };
}
