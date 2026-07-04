using System.Reflection;
using ModelContextProtocol.Server;
using Shouldly;

namespace RoslynLens.Tests.Tools;

/// <summary>
/// Guards the MCP annotations (readOnlyHint / idempotentHint / openWorldHint) that every
/// tool advertises to clients. These let hosts reason about permissions and caching, so a
/// tool silently losing its annotation would be a real regression.
/// </summary>
public class ToolAnnotationsTests
{
    private static IEnumerable<(string Name, McpServerToolAttribute Attr)> AllTools()
    {
        var assembly = typeof(ListSolutionsTool).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr is not null)
                    yield return (attr.Name ?? method.Name, attr);
            }
        }
    }

    private static bool? BoolProp(McpServerToolAttribute attr, string name) =>
        (bool?)attr.GetType().GetProperty(name)!.GetValue(attr);

    [Fact]
    public void Discovers_the_full_tool_surface()
    {
        AllTools().Select(t => t.Name).Distinct().Count().ShouldBeGreaterThan(30);
    }

    [Fact]
    public void Every_tool_is_closed_world()
    {
        foreach (var (name, attr) in AllTools())
            BoolProp(attr, "OpenWorld").ShouldBe(false, $"{name} analyses local code — OpenWorld must be false");
    }

    [Fact]
    public void Only_switch_solution_is_write_capable()
    {
        foreach (var (name, attr) in AllTools())
        {
            if (name == "switch_solution")
                BoolProp(attr, "ReadOnly").ShouldBe(false, "switch_solution mutates workspace state");
            else
                BoolProp(attr, "ReadOnly").ShouldBe(true, $"{name} must advertise ReadOnly");
        }
    }
}
