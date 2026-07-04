using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RoslynLens;

/// <summary>
/// Wraps every <c>tools/call</c> request in a correlation-scoped log entry recording the
/// tool name, a short per-call correlation id, wall-clock duration, and success/failure.
/// Correlation ids let a client-visible error be traced back to a specific server log line,
/// and the duration makes it trivial to spot slow tools without extra instrumentation.
/// All output goes to stderr (stdout is reserved for JSON-RPC).
/// </summary>
public static class ToolInvocationLogging
{
    public static IMcpServerBuilder WithToolInvocationLogging(this IMcpServerBuilder builder)
    {
        return builder.WithRequestFilters(filters =>
            filters.AddCallToolFilter(next => async (ctx, ct) =>
            {
                var toolName = ctx.Params?.Name ?? "(unknown)";
                var correlationId = Guid.NewGuid().ToString("N")[..8];
                var logger = ctx.Services?
                    .GetService<ILoggerFactory>()?
                    .CreateLogger("RoslynLens.Tool");

                using var scope = logger?.BeginScope("cid:{CorrelationId}", correlationId);
                var stopwatch = Stopwatch.StartNew();
                logger?.LogInformation("→ {Tool} started [cid {CorrelationId}]", toolName, correlationId);

                try
                {
                    var result = await next(ctx, ct);
                    stopwatch.Stop();
                    logger?.LogInformation(
                        "← {Tool} completed in {ElapsedMs}ms [cid {CorrelationId}]",
                        toolName, stopwatch.ElapsedMilliseconds, correlationId);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    logger?.LogWarning(
                        "⊘ {Tool} cancelled after {ElapsedMs}ms [cid {CorrelationId}]",
                        toolName, stopwatch.ElapsedMilliseconds, correlationId);
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logger?.LogError(
                        ex, "✗ {Tool} failed after {ElapsedMs}ms [cid {CorrelationId}]",
                        toolName, stopwatch.ElapsedMilliseconds, correlationId);
                    throw;
                }
            }));
    }
}
