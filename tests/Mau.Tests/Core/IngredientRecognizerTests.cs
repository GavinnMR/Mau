using Mau.Core.Engine;

namespace Mau.Tests.Core;

public class IngredientRecognizerTests
{
    private static IngredientRecognizer CreateRecognizer() =>
        new(new[]
        {
            new LexiconEntry("chicken", 1),
            new LexiconEntry("manok", 1),
            new LexiconEntry("pork", 2),
            new LexiconEntry("baboy", 2),
            new LexiconEntry("soy sauce", 3),
            new LexiconEntry("toyo", 3),
            new LexiconEntry("garlic", 5),
            new LexiconEntry("bawang", 5),
            new LexiconEntry("salt", 8),
            new LexiconEntry("asin", 8),
            new LexiconEntry("sibuyas", 6),
            new LexiconEntry("suka", 4),
        });

    [Fact]
    public void Parse_CommaSeparatedKnownTokens_ReturnsTokensAndNoHints()
    {
        var result = CreateRecognizer().Parse("chicken, toyo,");

        AssertTokens(result,
            (0, "chicken", "chicken", 1),
            (1, "toyo", "toyo", 3));
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_UnknownTokenAndPendingPrefix_ReturnsHints()
    {
        var result = CreateRecognizer().Parse("chicken, chiken, ba");

        AssertTokens(result,
            (0, "chicken", "chicken", 1),
            (1, "chiken", "chiken", (int?)null));
        Assert.Equal("ba", result.PendingPrefix);
        AssertHints(result, ("baboy", 2), ("bawang", 5));
    }

    [Fact]
    public void Parse_SingleWordWithoutDelimiter_ReturnsNoTokensAndHint()
    {
        var result = CreateRecognizer().Parse("chicken");

        Assert.Empty(result.Tokens);
        Assert.Equal("chicken", result.PendingPrefix);
        AssertHints(result, ("chicken", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_EmptyText_ReturnsNothing(string? text)
    {
        var result = CreateRecognizer().Parse(text);

        Assert.Empty(result.Tokens);
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_OnlyDelimitersAndSpaces_ReturnsNothing()
    {
        var result = CreateRecognizer().Parse(",,  ,");

        Assert.Empty(result.Tokens);
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_RawKeepsCaseAndSpacesWhileNormalizedCollapses()
    {
        var result = CreateRecognizer().Parse("  Soy   SAUCE ,");

        AssertTokens(result, (0, "Soy   SAUCE", "soy sauce", 3));
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_TwoKeysForSameIngredient_ReturnsBothTokens()
    {
        var result = CreateRecognizer().Parse("toyo, soy sauce,");

        AssertTokens(result,
            (0, "toyo", "toyo", 3),
            (1, "soy sauce", "soy sauce", 3));
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_NewlineDelimiters_ReturnsTokens()
    {
        var result = CreateRecognizer().Parse("chicken\npork\n");

        AssertTokens(result,
            (0, "chicken", "chicken", 1),
            (1, "pork", "pork", 2));
        Assert.Equal("", result.PendingPrefix);
        Assert.Empty(result.Hints);
    }

    [Fact]
    public void Parse_SingleLetterPrefix_ReturnsHintsInOrder()
    {
        var result = CreateRecognizer().Parse("s");

        Assert.Empty(result.Tokens);
        Assert.Equal("s", result.PendingPrefix);
        AssertHints(result,
            ("salt", 8),
            ("sibuyas", 6),
            ("soy sauce", 3),
            ("suka", 4));
    }

    [Fact]
    public void GetHints_UppercasePrefix_IsCaseInsensitive()
    {
        var hints = CreateRecognizer().GetHints("CHI");

        AssertHints(hints, ("chicken", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("zzz")]
    public void GetHints_EmptyOrUnknownPrefix_ReturnsEmpty(string? prefix)
    {
        var hints = CreateRecognizer().GetHints(prefix);

        Assert.Empty(hints);
    }

    [Fact]
    public void GetHints_MoreThanEightMatches_ReturnsAtMostEight()
    {
        var entries = Enumerable.Range(1, 9).Select(i => new LexiconEntry($"aa{i}", i));
        var recognizer = new IngredientRecognizer(entries);

        var hints = recognizer.GetHints("aa");

        Assert.Equal(8, IngredientRecognizer.MaxHints);
        Assert.Equal(8, hints.Count);
    }

    private static void AssertTokens(
        ParseResult result,
        params (int Index, string Raw, string Normalized, int? IngredientId)[] expected)
    {
        Assert.Equal(expected, result.Tokens.Select(t => (t.Index, t.Raw, t.Normalized, t.IngredientId)));
    }

    private static void AssertHints(
        ParseResult result,
        params (string Text, int IngredientId)[] expected)
    {
        AssertHints(result.Hints, expected);
    }

    private static void AssertHints(
        IReadOnlyList<LexiconHint> hints,
        params (string Text, int IngredientId)[] expected)
    {
        Assert.Equal(expected, hints.Select(h => (h.Text, h.IngredientId)));
    }
}
