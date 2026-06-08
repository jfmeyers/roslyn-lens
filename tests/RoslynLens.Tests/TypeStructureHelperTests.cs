using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests;

public class TypeStructureHelperTests
{
    private static INamedTypeSymbol CompileType(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return compilation.GlobalNamespace.GetNamespaceMembers()
            .SelectMany(ns => ns.GetTypeMembers())
            .First();
    }

    [Fact]
    public void CollectStructuralTypeRefs_ReturnsBaseType()
    {
        var type = CompileType("""
            namespace A { public class Base { } }
            namespace B { public class Child : A.Base { } }
            """);

        // The test type is A.Base (first namespace); use the child
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("""
                namespace B { public class Child : System.Collections.Generic.List<string> { } }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)]);

        var childType = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers().First();

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(childType).ToList();
        refs.ShouldContain(t => t.Name == "List");
    }

    [Fact]
    public void CollectStructuralTypeRefs_ReturnsFieldType()
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("""
                namespace A
                {
                    public class Dep { }
                    public class Owner { public Dep Field; }
                }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var owner = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers()
            .First(t => t.Name == "Owner");

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(owner).ToList();
        refs.ShouldContain(t => t.Name == "Dep");
    }

    [Fact]
    public void CollectStructuralTypeRefs_ReturnsPropertyType()
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("""
                namespace A
                {
                    public class Dep { }
                    public class Owner { public Dep Prop { get; set; } }
                }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var owner = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers()
            .First(t => t.Name == "Owner");

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(owner).ToList();
        refs.ShouldContain(t => t.Name == "Dep");
    }

    [Fact]
    public void CollectStructuralTypeRefs_ReturnsMethodReturnAndParameterTypes()
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("""
                namespace A
                {
                    public class ReturnDep { }
                    public class ParamDep { }
                    public class Owner { public ReturnDep Method(ParamDep p) => null; }
                }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var owner = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers()
            .First(t => t.Name == "Owner");

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(owner).Select(t => t.Name).ToList();
        refs.ShouldContain("ReturnDep");
        refs.ShouldContain("ParamDep");
    }

    [Fact]
    public void CollectStructuralTypeRefs_ReturnsInterface()
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("""
                namespace A
                {
                    public interface IService { }
                    public class Impl : IService { }
                }
                """)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var impl = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers()
            .First(t => t.Name == "Impl");

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(impl).Select(t => t.Name).ToList();
        refs.ShouldContain("IService");
    }

    [Fact]
    public void CollectStructuralTypeRefs_EmptyForIsolatedType()
    {
        var compilation = CSharpCompilation.Create(
            "Test",
            [CSharpSyntaxTree.ParseText("namespace A { public class Standalone { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var type = compilation.GlobalNamespace
            .GetNamespaceMembers().First()
            .GetTypeMembers().First();

        var refs = TypeStructureHelper.CollectStructuralTypeRefs(type).ToList();
        refs.ShouldNotContain(t => t.ContainingNamespace.Name == "A");
    }

    [Theory]
    [InlineData("System.Collections")]
    [InlineData("System.Threading")]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("Microsoft.AspNetCore.Mvc")]
    public void IsSystemNamespace_ReturnsTrueForFrameworkNamespaces(string ns)
    {
        TypeStructureHelper.IsSystemNamespace(ns).ShouldBeTrue();
    }

    [Theory]
    [InlineData("MyApp.Services")]
    [InlineData("Granit.Core")]
    [InlineData("RoslynLens")]
    public void IsSystemNamespace_ReturnsFalseForUserNamespaces(string ns)
    {
        TypeStructureHelper.IsSystemNamespace(ns).ShouldBeFalse();
    }
}
