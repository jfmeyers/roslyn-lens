using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

/// <summary>
/// Base class for detectors that match method invocation expressions
/// (e.g. Guid.NewGuid(), Thread.Sleep()).
/// </summary>
public abstract class InvocationDetectorBase : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    protected abstract IReadOnlyList<string> TargetExpressions { get; }
    protected abstract string Id { get; }
    protected abstract string Message { get; }
    protected abstract string Suggestion { get; }

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath;

        var matches = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => TargetExpressions.Contains(invocation.Expression.ToString(), StringComparer.Ordinal));

        foreach (var invocation in matches)
        {
            ct.ThrowIfCancellationRequested();
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new AntiPatternViolation(
                Id, AntiPatternSeverity.Warning, Message, filePath, line, Suggestion);
        }
    }
}
