using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class HardcodedSecretDetectorTests
{
    private readonly HardcodedSecretDetector _detector = new();

    [Fact]
    public void Detects_Hardcoded_Password_In_Variable()
    {
        const string source = """
            public class Config
            {
                public void Setup()
                {
                    var password = "SuperSecret123";
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-SECRET");
    }

    [Fact]
    public void Detects_Hardcoded_Secret_In_Assignment()
    {
        const string source = """
            public class Config
            {
                public string ApiKey { get; set; }
                public void Setup()
                {
                    ApiKey = "sk-1234567890";
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-SECRET");
    }

    [Fact]
    public void Detects_Hardcoded_Secret_In_Object_Initializer()
    {
        const string source = """
            public class DbSettings
            {
                public string ConnectionString { get; set; }
            }
            public class Config
            {
                public void Setup()
                {
                    var settings = new DbSettings { ConnectionString = "Server=prod;Password=secret" };
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-SECRET");
    }

    [Fact]
    public void Ignores_Placeholder_Values()
    {
        const string source = """
            public class Config
            {
                public void Setup()
                {
                    var password = "${ENV_PASSWORD}";
                    var secret = "CHANGE_ME";
                    var token = "{vault:token}";
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Empty_Values()
    {
        const string source = """
            public class Config
            {
                public void Setup()
                {
                    var password = "";
                    var secret = "   ";
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Non_Sensitive_Names()
    {
        const string source = """
            public class Config
            {
                public void Setup()
                {
                    var username = "admin";
                    var label = "hello";
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
