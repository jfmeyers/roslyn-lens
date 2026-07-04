using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynLens;

/// <summary>
/// Lazily loads the Roslynator C# diagnostic analyzers that ship next to the tool (under
/// <c>roslynator/</c>). The analyzer assemblies are built against an older Roslyn and depend
/// on renamed sibling DLLs, so a small <see cref="IAnalyzerAssemblyLoader"/> resolves those
/// dependencies by assembly name from the same folder. Loading is best-effort: any failure
/// yields an empty set, and <c>get_diagnostics</c> transparently falls back to compiler-only
/// diagnostics.
/// </summary>
internal static class RoslynatorAnalyzers
{
    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> Lazy = new(Load);

    public static ImmutableArray<DiagnosticAnalyzer> Analyzers => Lazy.Value;

    private static ImmutableArray<DiagnosticAnalyzer> Load()
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "roslynator");
            var analyzerDll = Path.Combine(directory, "Roslynator.CSharp.Analyzers.dll");
            if (!File.Exists(analyzerDll))
                return ImmutableArray<DiagnosticAnalyzer>.Empty;

            var loader = new DirectoryAssemblyLoader(directory);
            var reference = new AnalyzerFileReference(analyzerDll, loader);
            return reference.GetAnalyzers(LanguageNames.CSharp);
        }
        catch
        {
            return ImmutableArray<DiagnosticAnalyzer>.Empty;
        }
    }

    /// <summary>
    /// Resolves Roslynator's renamed dependency assemblies by simple name from a single
    /// directory. Only names it knows about are handled, so the host's own assemblies
    /// (including the running Roslyn) continue to resolve normally.
    /// </summary>
    private sealed class DirectoryAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);

        public DirectoryAssemblyLoader(string directory)
        {
            foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
                Register(dll);

            AssemblyLoadContext.Default.Resolving += (context, name) =>
                name.Name is { } simpleName && _byName.TryGetValue(simpleName, out var path)
                    ? context.LoadFromAssemblyPath(path)
                    : null;
        }

        public void AddDependencyLocation(string fullPath) => Register(fullPath);

        public Assembly LoadFromPath(string fullPath) =>
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);

        private void Register(string dll)
        {
            try
            {
                _byName[AssemblyName.GetAssemblyName(dll).Name!] = dll;
            }
            catch
            {
                // Unreadable/non-managed DLL — skip; the analyzer simply won't resolve it.
            }
        }
    }
}
