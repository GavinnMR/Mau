using Mau.Core.Engine;

namespace Mau.Tests.Core;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Soy   SAUCE ", "soy sauce")]
    [InlineData("Toyo", "toyo")]
    [InlineData("a\tb", "a b")]
    [InlineData("   ", "")]
    public void Normalize_Input_ReturnsNormalizedText(string input, string expected)
    {
        var result = TextNormalizer.Normalize(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_Null_ReturnsEmptyString()
    {
        var result = TextNormalizer.Normalize(null);

        Assert.Equal("", result);
    }
}
