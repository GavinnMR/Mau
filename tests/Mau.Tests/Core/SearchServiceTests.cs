using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class SearchServiceTests
{
    [Fact]
    public async Task Search_ChickenOnly_ReturnsDishesOrderedByMissingThenTime()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken,", null));

        AssertDishes(result,
            ("Garlic fried chicken", new[] { "garlic" }),
            ("Chicken tinola", new[] { "ginger", "onion", "garlic" }),
            ("Chicken adobo", new[] { "soy sauce", "vinegar", "garlic" }));
    }

    [Fact]
    public async Task Search_ChickenAndGarlic_NarrowsStillNeeded()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken, garlic,", null));

        AssertDishes(result,
            ("Garlic fried chicken", Array.Empty<string>()),
            ("Chicken tinola", new[] { "ginger", "onion" }),
            ("Chicken adobo", new[] { "soy sauce", "vinegar" }));
    }

    [Fact]
    public async Task Search_GarlicExcludingPork_ReturnsPorkFreeDishes()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("garlic,", new[] { 2 }));

        AssertDishes(result,
            ("Garlic fried chicken", new[] { "chicken" }),
            ("Chicken tinola", new[] { "chicken", "ginger", "onion" }),
            ("Chicken adobo", new[] { "chicken", "soy sauce", "vinegar" }));
    }

    [Fact]
    public async Task Search_ChickenAndPork_ReturnsNoDishes()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken, pork,", null));

        Assert.Empty(result.Dishes);
    }

    [Fact]
    public async Task Search_ChickenAndPorkWithPorkExcluded_MatchesChickenOnlyAndMarksPorkExcluded()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken, pork,", new[] { 2 }));

        AssertDishes(result,
            ("Garlic fried chicken", new[] { "garlic" }),
            ("Chicken tinola", new[] { "ginger", "onion", "garlic" }),
            ("Chicken adobo", new[] { "soy sauce", "vinegar", "garlic" }));
        Assert.Equal(2, result.Tokens[1].IngredientId);
        Assert.True(result.Tokens[1].IsExcluded);
    }

    [Fact]
    public async Task Search_StapleSalt_ReturnsDishesContainingSalt()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("salt,", null));

        AssertDishes(result,
            ("Garlic fried chicken", new[] { "chicken", "garlic" }),
            ("Chicken tinola", new[] { "chicken", "ginger", "onion", "garlic" }));
    }

    [Fact]
    public async Task Search_ChickenAndUnknown_IgnoresUnknownToken()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken, dragonfruit,", null));

        AssertDishes(result,
            ("Garlic fried chicken", new[] { "garlic" }),
            ("Chicken tinola", new[] { "ginger", "onion", "garlic" }),
            ("Chicken adobo", new[] { "soy sauce", "vinegar", "garlic" }));
        Assert.Null(result.Tokens[1].IngredientId);
        Assert.Null(result.Tokens[1].IngredientName);
    }

    [Fact]
    public async Task Search_SynonymAndCanonicalForSameIngredient_CollapsesIncludeSet()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("toyo, soy sauce,", null));

        AssertDishes(result,
            ("Chicken adobo", new[] { "chicken", "vinegar", "garlic" }),
            ("Pork adobo", new[] { "pork", "vinegar", "garlic" }));
    }

    [Fact]
    public async Task Search_EmptyText_ReturnsNoDishes()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("", null));

        Assert.Empty(result.Tokens);
        Assert.Empty(result.Dishes);
    }

    [Fact]
    public async Task Search_UnknownOnly_ReturnsNoDishes()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("dragonfruit,", null));

        Assert.Empty(result.Dishes);
    }

    [Fact]
    public async Task Search_OnlyIncludedIngredientExcluded_ReturnsNoDishes()
    {
        var service = await CreateServiceAsync();

        var result = service.Search(new SearchRequest("chicken,", new[] { 1 }));

        Assert.Empty(result.Dishes);
        Assert.True(result.Tokens[0].IsExcluded);
    }

    [Fact]
    public async Task GetHints_Ba_ReturnsPorkAndGarlicHints()
    {
        var service = await CreateServiceAsync();

        var hints = service.GetHints("ba");

        Assert.Equal(
            new[] { ("baboy", 2, "pork"), ("bawang", 5, "garlic"), ("bay leaf", 7, "bay leaf") },
            hints.Select(h => (h.Text, h.IngredientId, h.IngredientName)).ToArray());
    }

    [Fact]
    public async Task GetHints_UppercaseChi_ReturnsChickenHint()
    {
        var service = await CreateServiceAsync();

        var hints = service.GetHints("CHI");

        Assert.Equal(
            new[] { ("chicken", 1, "chicken") },
            hints.Select(h => (h.Text, h.IngredientId, h.IngredientName)).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("zzz")]
    public async Task GetHints_EmptyOrUnknown_ReturnsEmpty(string? prefix)
    {
        var service = await CreateServiceAsync();

        var hints = service.GetHints(prefix);

        Assert.Empty(hints);
    }

    [Fact]
    public async Task GetDish_ExistingId_ReturnsDish()
    {
        var service = await CreateServiceAsync();

        var dish = service.GetDish(1);

        Assert.NotNull(dish);
        Assert.Equal("Chicken adobo", dish!.Name);
    }

    [Fact]
    public async Task GetDish_MissingId_ReturnsNull()
    {
        var service = await CreateServiceAsync();

        var dish = service.GetDish(999);

        Assert.Null(dish);
    }

    private static async Task<SearchService> CreateServiceAsync()
    {
        var store = new CatalogStore(new FakeCatalogLoader(TestCatalog.CreateSnapshot()));
        await store.ReloadAsync();
        return new SearchService(store);
    }

    private static void AssertDishes(
        SearchResult result,
        params (string Name, string[] StillNeeded)[] expected)
    {
        Assert.Equal(expected.Length, result.Dishes.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, result.Dishes[i].Name);
            Assert.Equal(expected[i].StillNeeded, result.Dishes[i].StillNeeded.ToArray());
        }
    }
}
