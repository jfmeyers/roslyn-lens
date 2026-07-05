using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class AnalyzeDataFlowTool
{
    [McpServerTool(Name = "analyze_data_flow", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Analyze data flow within a method: variable declarations, reads, writes, captured variables, and data flowing in/out.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to analyze")] string methodName,
        [Description("Optional containing class name")] string? className = null,
        CancellationToken ct = default)
    {
        var (err, model, body) = await SymbolResolver.ResolveMethodBodyOrErrorAsync(workspace, methodName, className, ct);
        if (err is not null) return err;

        DataFlowAnalysis? analysis = null;

        if (body is BlockSyntax block && block.Statements.Count > 0)
        {
            analysis = model.AnalyzeDataFlow(block.Statements.First(), block.Statements.Last());
        }
        else if (body is ArrowExpressionClauseSyntax arrow)
        {
            analysis = model.AnalyzeDataFlow(arrow.Expression);
        }

        if (analysis?.Succeeded != true)
            return Json.Serialize(new { error = "Data flow analysis failed" });

        var result = new DataFlowResult(
            methodName,
            analysis.VariablesDeclared.Select(s => s.Name).ToList(),
            analysis.DataFlowsIn.Select(s => s.Name).ToList(),
            analysis.DataFlowsOut.Select(s => s.Name).ToList(),
            analysis.ReadInside.Select(s => s.Name).ToList(),
            analysis.WrittenInside.Select(s => s.Name).ToList(),
            analysis.AlwaysAssigned.Select(s => s.Name).ToList(),
            analysis.Captured.Select(s => s.Name).ToList());

        return Json.Serialize(result);
    }
}
