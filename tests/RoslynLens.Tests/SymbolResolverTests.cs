using Shouldly;

namespace RoslynLens.Tests;

public class SymbolResolverTests
{
    [Theory]
    [InlineData("hello", "world", 4)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "abd", 1)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("SymbolResolver", "SymbolResolvr", 1)]
    [InlineData("WorkspaceManager", "WorkspaceManger", 1)]
    public void LevenshteinDistance_ComputesCorrectly(string a, string b, int expected)
    {
        SymbolResolver.LevenshteinDistance(a, b).ShouldBe(expected);
    }

    [Fact]
    public void LevenshteinDistance_IsCaseInsensitive()
    {
        SymbolResolver.LevenshteinDistance("Hello", "hello").ShouldBe(0);
    }
}
