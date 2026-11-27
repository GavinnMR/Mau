using Mau.Core.Engine;

namespace Mau.Tests.Core;

public class FuzzyMatcherTests
{
    private static readonly FuzzyOptions Options = new();

    private static readonly FuzzyMatcher Matcher = new(new[]
    {
        new LexiconEntry("chicken", 1),
        new LexiconEntry("manok", 1),
        new LexiconEntry("pork", 2),
        new LexiconEntry("baboy", 2),
        new LexiconEntry("soy sauce", 3),
        new LexiconEntry("toyo", 3),
        new LexiconEntry("calamansi", 4),
        new LexiconEntry("tomato", 5),
        new LexiconEntry("potato", 6),
    });

    [Theory]
    [InlineData("chiken", "chicken")]
    [InlineData("CHICKEN ", null)]
    [InlineData("baboi", "baboy")]
    [InlineData("toyoo", "toyo")]
    [InlineData("soysauce", "soy sauce")]
    [InlineData("kalamansi", "calamansi")]
    public void Suggest_FindsTheClosestName(string word, string? expected)
    {
        var suggestions = Matcher.Suggest(word, Options);

        if (expected is null)
        {
            Assert.Empty(suggestions);
        }
        else
        {
            Assert.Equal(expected, suggestions[0].Text);
        }
    }

    [Theory]
    [InlineData("chikn")]
    [InlineData("xyz")]
    [InlineData("")]
    [InlineData("chicken thigh")]
    public void Suggest_TooFarOrEmpty_ReturnsNothing(string word)
    {
        Assert.Empty(Matcher.Suggest(word, Options));
    }

    [Fact]
    public void Suggest_ShortWordsAllowOneLetter_LongWordsTwo()
    {
        Assert.Empty(Matcher.Suggest("chikem", Options));
        Assert.Equal("calamansi", Assert.Single(Matcher.Suggest("kalamanse", Options)).Text);
    }

    [Fact]
    public void Suggest_SeveralCloseNames_ClosestFirstAndCapped()
    {
        var suggestions = Matcher.Suggest("potomato", new FuzzyOptions { MaxDistanceLongWords = 3 });

        // Both are 2 edits away; equal distances are listed alphabetically.
        Assert.Equal(new[] { "potato", "tomato" }, suggestions.Select(item => item.Text));
    }

    [Fact]
    public void Suggest_OnePerIngredient_AndRespectsMaxSuggestions()
    {
        var wide = new FuzzyOptions { MaxDistance = 5, MaxSuggestions = 2 };

        var suggestions = Matcher.Suggest("porc", wide);

        Assert.Equal(2, suggestions.Count);
        Assert.Equal(2, suggestions.Select(item => item.IngredientId).Distinct().Count());
        Assert.Equal("pork", suggestions[0].Text);
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("chicken", "chicken", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("toyo", "toyoo", 1)]
    public void Distance_IsLevenshtein(string a, string b, int expected)
    {
        Assert.Equal(expected, FuzzyMatcher.Distance(a, b, 10));
    }

    [Fact]
    public void Distance_StopsEarlyAboveTheLimit()
    {
        Assert.Equal(2, FuzzyMatcher.Distance("kitten", "sitting", 1));
    }
}
