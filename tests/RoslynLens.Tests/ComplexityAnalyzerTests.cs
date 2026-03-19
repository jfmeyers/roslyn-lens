using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;

namespace RoslynLens.Tests;

public class ComplexityAnalyzerTests
{
    private static MethodDeclarationSyntax ParseMethod(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    public void CyclomaticComplexity_SimpleMethod_Returns1()
    {
        var method = ParseMethod("""
            class C {
                void M() { var x = 1; }
            }
            """);

        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(1);
    }

    [Fact]
    public void CyclomaticComplexity_WithBranches_CountsDecisionPoints()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    if (x > 0) return 1;
                    else if (x < 0) return -1;
                    else return 0;
                }
            }
            """);

        // base(1) + 2 ifs = 3
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void CyclomaticComplexity_WithLogicalOperators_CountsThem()
    {
        var method = ParseMethod("""
            class C {
                bool M(int x) {
                    if (x > 0 && x < 10 || x == -1) return true;
                    return false;
                }
            }
            """);

        // base(1) + if(1) + &&(1) + ||(1) = 4
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CognitiveComplexity_NestedIfs_HigherThanFlat()
    {
        var flat = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) { }
                    if (x < 10) { }
                }
            }
            """);

        var nested = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) {
                        if (x < 10) { }
                    }
                }
            }
            """);

        var flatScore = ComplexityAnalyzer.CalculateCognitiveComplexity(flat.Body!);
        var nestedScore = ComplexityAnalyzer.CalculateCognitiveComplexity(nested.Body!);

        nestedScore.ShouldBeGreaterThan(flatScore);
    }

    [Fact]
    public void MaxNestingDepth_FlatMethod_Returns0()
    {
        var method = ParseMethod("""
            class C {
                void M() { var x = 1; }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBe(0);
    }

    [Fact]
    public void MaxNestingDepth_TripleNested_Returns3()
    {
        var method = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) {
                        for (int i = 0; i < x; i++) {
                            while (true) {
                                break;
                            }
                        }
                    }
                }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBe(3);
    }

    [Fact]
    public void LogicalLoc_ExcludesBlanksAndComments()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    // This is a comment
                    var x = 1;

                    var y = 2;
                    // Another comment
                    var z = x + y;
                }
            }
            """);

        // Should count: { var x = 1; var y = 2; var z = x + y; } = 5 lines (opening brace + 3 statements + closing brace)
        var loc = ComplexityAnalyzer.CalculateLogicalLoc(method.Body!);
        loc.ShouldBe(5);
    }

    [Fact]
    public void Analyze_ReturnsAllMetrics()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    if (x > 0) return 1;
                    return 0;
                }
            }
            """);

        var metrics = ComplexityAnalyzer.Analyze(method.Body!);

        metrics.Cyclomatic.ShouldBeGreaterThanOrEqualTo(2);
        metrics.Cognitive.ShouldBeGreaterThanOrEqualTo(1);
        metrics.MaxNesting.ShouldBeGreaterThanOrEqualTo(0);
        metrics.LogicalLoc.ShouldBeGreaterThan(0);
    }
}
