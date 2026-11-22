using Mau.Core.Catalog;
using Mau.Core.Engine;

namespace Mau.Tests.Core;

public class IngredientTrieTests
{
    [Fact]
    public void TryGetExact_AddedKey_ReturnsTrueAndId()
    {
        var trie = new IngredientTrie();
        trie.Add("chicken", 1);

        var found = trie.TryGetExact("chicken", out var id);

        Assert.True(found);
        Assert.Equal(1, id);
    }

    [Fact]
    public void TryGetExact_MissingKey_ReturnsFalse()
    {
        var trie = new IngredientTrie();
        trie.Add("chicken", 1);

        var found = trie.TryGetExact("pork", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetExact_PrefixOfKey_ReturnsFalse()
    {
        var trie = new IngredientTrie();
        trie.Add("chicken", 1);

        var found = trie.TryGetExact("chick", out _);

        Assert.False(found);
    }

    [Fact]
    public void GetPrefixMatches_CommonPrefix_ReturnsKeysInOrdinalOrder()
    {
        var trie = new IngredientTrie();
        trie.Add("bawang", 5);
        trie.Add("baboy", 2);
        trie.Add("asin", 8);

        var matches = trie.GetPrefixMatches("ba", 8);

        Assert.Equal(new[] { ("baboy", 2), ("bawang", 5) }, matches);
    }

    [Fact]
    public void GetPrefixMatches_KeyThatIsPrefixOfAnother_ReturnsShorterKeyFirst()
    {
        var trie = new IngredientTrie();
        trie.Add("salt", 1);
        trie.Add("salted", 2);
        trie.Add("sal", 3);

        var matches = trie.GetPrefixMatches("sal", 8);

        Assert.Equal(new[] { ("sal", 3), ("salt", 1), ("salted", 2) }, matches);
    }

    [Fact]
    public void GetPrefixMatches_MaxLimit_ReturnsAtMostMax()
    {
        var trie = new IngredientTrie();
        trie.Add("salt", 1);
        trie.Add("salted", 2);
        trie.Add("sal", 3);

        var matches = trie.GetPrefixMatches("sal", 1);

        Assert.Single(matches);
        Assert.Equal("sal", matches[0].Text);
    }

    [Fact]
    public void GetPrefixMatches_UnknownPrefix_ReturnsEmpty()
    {
        var trie = new IngredientTrie();
        trie.Add("chicken", 1);

        var matches = trie.GetPrefixMatches("zzz", 8);

        Assert.Empty(matches);
    }

    [Fact]
    public void GetPrefixMatches_EmptyPrefix_ReturnsEmpty()
    {
        var trie = new IngredientTrie();
        trie.Add("chicken", 1);

        var matches = trie.GetPrefixMatches("", 8);

        Assert.Empty(matches);
    }

    [Fact]
    public void Add_SameKeyDifferentId_ThrowsCatalogException()
    {
        var trie = new IngredientTrie();
        trie.Add("toyo", 3);

        var ex = Assert.Throws<CatalogException>(() => trie.Add("toyo", 4));

        Assert.Contains("toyo", ex.Message);
    }

    [Fact]
    public void Add_SameKeySameId_DoesNotThrow()
    {
        var trie = new IngredientTrie();
        trie.Add("toyo", 3);

        var exception = Record.Exception(() => trie.Add("toyo", 3));

        Assert.Null(exception);
    }
}
