using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RoslynLens;

/// <summary>
/// Cross-project symbol resolution with disambiguation by file path and line number.
/// </summary>
public static class SymbolResolver
{
    public static async Task<IReadOnlyList<ISymbol>> FindSymbolsByNameAsync(
        WorkspaceManager workspace,
        string name,
        string? kind = null,
        CancellationToken ct = default)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var isGlob = name.Contains('*') || name.Contains('?');
        Func<string, bool> namePredicate;

        if (isGlob)
        {
            var pattern = "^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            namePredicate = n => regex.IsMatch(n);
        }
        else
        {
            namePredicate = n => n.Equals(name, StringComparison.Ordinal) ||
                                 n.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        var results = new List<ISymbol>();
        var seen = new HashSet<string>();

        foreach (var project in solution.Projects)
        {
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            var symbols = compilation.GetSymbolsWithName(namePredicate, SymbolFilter.All, ct);

            foreach (var symbol in symbols)
            {
                if (kind is not null && !MatchesKind(symbol, kind))
                    continue;

                var displayString = symbol.ToDisplayString();
                if (seen.Add(displayString))
                    results.Add(symbol);
            }
        }

        return results;
    }

    public static async Task<ISymbol?> ResolveSymbolAsync(
        WorkspaceManager workspace,
        string name,
        string? filePath = null,
        int? line = null,
        string? kind = null,
        CancellationToken ct = default)
    {
        var candidates = await FindSymbolsByNameAsync(workspace, name, kind, ct);

        if (candidates.Count == 0)
        {
            var fuzzy = await FindSymbolsFuzzyAsync(workspace, name, kind, ct);
            return fuzzy.Count > 0 ? fuzzy[0].Symbol : null;
        }
        if (candidates.Count == 1) return candidates[0];

        // Disambiguate by file path suffix match
        if (filePath is not null)
        {
            var normalized = filePath.Replace('\\', '/');
            var match = candidates.FirstOrDefault(s =>
            {
                var loc = GetLocation(s);
                return loc.FilePath is not null &&
                       loc.FilePath.Replace('\\', '/').EndsWith(normalized, StringComparison.OrdinalIgnoreCase);
            });
            if (match is not null) return match;
        }

        // Disambiguate by line number
        if (line is not null)
        {
            var match = candidates.FirstOrDefault(s =>
            {
                var loc = GetLocation(s);
                return loc.Line == line;
            });
            if (match is not null) return match;
        }

        // Return first match
        return candidates[0];
    }

    public static (string? FilePath, int? Line) GetLocation(ISymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null) return (null, null);

        var span = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span);
        return (syntaxRef.SyntaxTree.FilePath, span.StartLinePosition.Line + 1);
    }

    public static bool MatchesKind(ISymbol symbol, string kind) =>
        kind.ToLowerInvariant() switch
        {
            "class" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class },
            "interface" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface },
            "struct" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct },
            "enum" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum },
            "record" => symbol is INamedTypeSymbol { IsRecord: true },
            "method" => symbol is IMethodSymbol,
            "property" => symbol is IPropertySymbol,
            "field" => symbol is IFieldSymbol,
            "event" => symbol is IEventSymbol,
            "namespace" => symbol is INamespaceSymbol,
            _ => true
        };

    public static async Task<ISymbol?> ResolveMethodByNameAsync(
        WorkspaceManager workspace,
        string methodName,
        string? className,
        CancellationToken ct)
    {
        var candidates = await FindSymbolsByNameAsync(workspace, methodName, "method", ct);

        if (className is not null)
        {
            candidates = candidates
                .Where(s => s.ContainingType?.Name.Equals(className, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    public static async Task<IReadOnlyList<ReferencedSymbol>> FindReferencesAsync(
        WorkspaceManager workspace,
        ISymbol symbol,
        CancellationToken ct)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
        return references.ToList();
    }

    public static async Task<IReadOnlyList<(ISymbol Symbol, string MatchQuality)>> FindSymbolsFuzzyAsync(
        WorkspaceManager workspace,
        string name,
        string? kind = null,
        CancellationToken ct = default)
    {
        // Step 1: exact match
        var exact = await FindSymbolsByNameAsync(workspace, name, kind, ct);
        if (exact.Count > 0)
            return exact.Select(s => (s, "exact")).ToList();

        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var candidates = new List<(ISymbol Symbol, string MatchQuality, int Score)>();

        // Extract the short name (last segment after '.')
        var shortName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;

        foreach (var project in solution.Projects)
        {
            var compilation = await workspace.GetCompilationAsync(project, ct);
            if (compilation is null) continue;

            // Step 2: partial namespace match — find by short name, then check FQN contains the query
            var symbols = compilation.GetSymbolsWithName(
                n => n.Equals(shortName, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.All, ct);

            foreach (var symbol in symbols)
            {
                if (kind is not null && !MatchesKind(symbol, kind)) continue;

                var fqn = symbol.ToDisplayString();

                if (name.Contains('.') && fqn.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((symbol, "partial_namespace", 1));
                }
                else if (shortName.Equals(symbol.Name, StringComparison.OrdinalIgnoreCase) && shortName != name)
                {
                    candidates.Add((symbol, "short_name", 2));
                }
            }

            // Step 3: Levenshtein for typo tolerance (only for non-dotted names)
            if (!name.Contains('.') && candidates.Count == 0)
            {
                var allSymbols = compilation.GetSymbolsWithName(_ => true, SymbolFilter.All, ct);
                foreach (var symbol in allSymbols)
                {
                    if (kind is not null && !MatchesKind(symbol, kind)) continue;

                    var distance = LevenshteinDistance(name, symbol.Name);
                    var maxAllowed = name.Length > 5 ? 2 : 1;
                    if (distance > 0 && distance <= maxAllowed)
                    {
                        candidates.Add((symbol, "fuzzy", 3 + distance));
                    }
                }
            }
        }

        // Deduplicate and sort by score
        var seen = new HashSet<string>();
        return candidates
            .OrderBy(c => c.Score)
            .Where(c => seen.Add(c.Symbol.ToDisplayString()))
            .Select(c => (c.Symbol, c.MatchQuality))
            .Take(10)
            .ToList();
    }

    public static async Task<(SemanticModel? Model, SyntaxNode? Body, SyntaxNode? MethodSyntax)> ResolveMethodBodyAsync(
        WorkspaceManager workspace,
        string methodName,
        string? className = null,
        CancellationToken ct = default)
    {
        var symbol = await ResolveMethodByNameAsync(workspace, methodName, className, ct);
        if (symbol is null) return (null, null, null);

        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null) return (null, null, null);

        var syntax = await syntaxRef.GetSyntaxAsync(ct);
        var solution = workspace.GetSolution();
        if (solution is null) return (null, null, syntax);

        var project = solution.Projects
            .FirstOrDefault(p => p.Documents.Any(d => d.FilePath == syntaxRef.SyntaxTree.FilePath));
        if (project is null) return (null, null, syntax);

        var compilation = await workspace.GetCompilationAsync(project, ct);
        if (compilation is null) return (null, null, syntax);

        var model = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

        SyntaxNode? body = syntax switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody,
            _ => null
        };

        return (model, body, syntax);
    }

    public static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
