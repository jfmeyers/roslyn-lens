using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests;

public class ActivationRootsTests
{
    // Stub the framework contracts in source so symbols resolve by full name
    // without referencing the real ASP.NET Core / hosting assemblies.
    private const string Contracts = """
        namespace Microsoft.Extensions.Hosting
        {
            public interface IHostedService { }
            public abstract class BackgroundService : IHostedService { }
        }
        namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }
        namespace Microsoft.AspNetCore.Mvc.RazorPages { public abstract class PageModel { } }
        namespace Microsoft.AspNetCore.Components { public abstract class ComponentBase { } }
        """;

    private static INamedTypeSymbol CompileType(string source, string typeName)
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(Contracts + source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .First();
    }

    [Fact]
    public void IsFrameworkActivated_ReturnsTrue_ForController() =>
        ActivationRoots.IsFrameworkActivated(
            CompileType("public class UsersController : Microsoft.AspNetCore.Mvc.ControllerBase { }", "UsersController"))
            .ShouldBeTrue();

    [Fact]
    public void IsFrameworkActivated_ReturnsTrue_ForBackgroundService() =>
        ActivationRoots.IsFrameworkActivated(
            CompileType("public class Worker : Microsoft.Extensions.Hosting.BackgroundService { }", "Worker"))
            .ShouldBeTrue();

    [Fact]
    public void IsFrameworkActivated_ReturnsTrue_ForDirectHostedServiceImpl() =>
        ActivationRoots.IsFrameworkActivated(
            CompileType("public class Job : Microsoft.Extensions.Hosting.IHostedService { }", "Job"))
            .ShouldBeTrue();

    [Fact]
    public void IsFrameworkActivated_ReturnsTrue_ThroughIntermediateBase()
    {
        var type = CompileType("""
            public abstract class ApiControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase { }
            public class OrdersController : ApiControllerBase { }
            """, "OrdersController");

        ActivationRoots.IsFrameworkActivated(type).ShouldBeTrue();
    }

    [Fact]
    public void IsFrameworkActivated_ReturnsFalse_ForPlainType() =>
        ActivationRoots.IsFrameworkActivated(
            CompileType("public class EmailSender { }", "EmailSender"))
            .ShouldBeFalse();

    [Fact]
    public void IsFrameworkActivated_ReturnsFalse_ForMembersOfActivatedType()
    {
        var type = CompileType(
            "public class Worker : Microsoft.Extensions.Hosting.BackgroundService { internal void Helper() { } }",
            "Worker");
        var method = type.GetMembers("Helper").Single();

        // Members keep normal analysis — only the type itself is rooted.
        ActivationRoots.IsFrameworkActivated(method).ShouldBeFalse();
    }
}
