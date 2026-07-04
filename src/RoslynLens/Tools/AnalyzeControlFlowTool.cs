using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class AnalyzeControlFlowTool
{
    [McpServerTool(Name = "analyze_control_flow", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Analyze control flow within a method: reachability, return points, and exit points.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to analyze")] string methodName,
        [Description("Optional containing class name")] string? className = null,
        CancellationToken ct = default)
    {
        var (err, model, body) = await SymbolResolver.ResolveMethodBodyOrErrorAsync(workspace, methodName, className, ct);
        if (err is not null) return err;

        if (body is not BlockSyntax block || block.Statements.Count == 0)
            return Json.Serialize(new { error = "Control flow analysis requires a block body with statements" });

        var analysis = model.AnalyzeControlFlow(block.Statements.First(), block.Statements.Last());
        if (analysis is null || !analysis.Succeeded)
            return Json.Serialize(new { error = "Control flow analysis failed" });

        var result = new ControlFlowResult(
            methodName,
            analysis.StartPointIsReachable,
            analysis.EndPointIsReachable,
            analysis.ReturnStatements.Length,
            analysis.ExitPoints.Length);

        return Json.Serialize(result);
    }
}
