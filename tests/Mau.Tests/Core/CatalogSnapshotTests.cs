using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class CatalogSnapshotTests
{
    [Fact]
    public void Create_Fixture_ReturnsTenIngredientsAndFourDishes()
    {
        var snapshot = TestCatalog.CreateSnapshot();

        Assert.Equal(10, snapshot.Ingredients.Count);
        Assert.Equal(4, snapshot.Dishes.Count);
    }

    [Fact]
    public void Create_Fixture_OrdersDishesByNameIgnoringCase()
    {
        var snapshot = TestCatalog.CreateSnapshot();

        Assert.Equal(
            new[] { "Chicken adobo", "Chicken tinola", "Garlic fried chicken", "Pork adobo" },
            snapshot.Dishes.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Create_Fixture_MapsDishIdsByIngredientId()
    {
        var snapshot = TestCatalog.CreateSnapshot();

        Assert.Equal(new[] { 1, 3, 4 }, snapshot.DishIdsByIngredientId[1].OrderBy(id => id).ToArray());
        Assert.Equal(new[] { 3 }, snapshot.DishIdsByIngredientId[10].OrderBy(id => id).ToArray());
    }

    [Fact]
    public void Create_IngredientUsedByNoDish_HasNoDishIdsEntry()
    {
        var ingredients = TestCatalog.CreateIngredients();
        ingredients.Add(new Ingredient
        {
            Id = 99,
            CanonicalName = "dragonfruit",
            Category = IngredientCategory.Other,
        });

        var snapshot = CatalogSnapshot.Create(ingredients, TestCatalog.CreateDishes(ingredients));

        Assert.False(snapshot.DishIdsByIngredientId.ContainsKey(99));
    }

    [Fact]
    public void Create_Fixture_RecognizerResolvesKnownSynonym()
    {
        var snapshot = TestCatalog.CreateSnapshot();

        var result = snapshot.Recognizer.Parse("toyo,");

        var token = Assert.Single(result.Tokens);
        Assert.Equal(3, token.IngredientId);
    }

    [Fact]
    public void Create_DishWithReversedRows_OrdersIngredientsByPositionAndCopiesStapleFlag()
    {
        var ingredients = TestCatalog.CreateIngredients();
        var dishes = TestCatalog.CreateDishes(ingredients);
        dishes.Single(d => d.Id == 3).Ingredients.Reverse();

        var snapshot = CatalogSnapshot.Create(ingredients, dishes);

        var dish = snapshot.DishesById[3];
        Assert.Equal(
            new[] { "chicken", "ginger", "onion", "garlic", "salt", "water" },
            dish.Ingredients.Select(i => i.Name).ToArray());
        Assert.True(dish.Ingredients.Single(i => i.Name == "salt").IsStaple);
        Assert.False(dish.Ingredients.Single(i => i.Name == "chicken").IsStaple);
    }

    [Fact]
    public void Create_DishSteps_SplitsOnNewlineAndRemovesBlankLines()
    {
        var snapshot = TestCatalog.CreateSnapshot();

        var dish = snapshot.DishesById[1];

        Assert.Equal(new[] { "Brown the chicken.", "Simmer until tender." }, dish.Steps);
    }

    [Fact]
    public void Create_SameNormalizedKeyOnTwoIngredients_ThrowsCatalogException()
    {
        var ingredients = TestCatalog.CreateIngredients();
        var extra = new Ingredient
        {
            Id = 99,
            CanonicalName = "dragonfruit",
            Category = IngredientCategory.Other,
        };
        extra.Synonyms.Add(new IngredientSynonym
        {
            IngredientId = 99,
            Ingredient = extra,
            Name = "Toyo",
            Language = SynonymLanguage.English,
        });
        ingredients.Add(extra);

        var ex = Assert.Throws<CatalogException>(
            () => CatalogSnapshot.Create(ingredients, TestCatalog.CreateDishes(ingredients)));

        Assert.Contains("toyo", ex.Message);
    }

    [Fact]
    public void Create_DishRowWithUnknownIngredientId_ThrowsCatalogException()
    {
        var ingredients = TestCatalog.CreateIngredients();
        var dishes = TestCatalog.CreateDishes(ingredients);
        dishes.Single(d => d.Id == 1).Ingredients.Add(new DishIngredient
        {
            DishId = 1,
            IngredientId = 999,
            Ingredient = new Ingredient { Id = 999, CanonicalName = "dragonfruit" },
            Position = 99,
        });

        Assert.Throws<CatalogException>(() => CatalogSnapshot.Create(ingredients, dishes));
    }

    [Fact]
    public void Current_BeforeReload_ThrowsInvalidOperationException()
    {
        var store = new CatalogStore(new FakeCatalogLoader(() => TestCatalog.CreateSnapshot()));

        var ex = Assert.Throws<InvalidOperationException>(() => store.Current);

        Assert.Equal("Catalog not loaded.", ex.Message);
    }

    [Fact]
    public async Task ReloadAsync_AfterLoad_CurrentReturnsLoadedSnapshot()
    {
        var expected = TestCatalog.CreateSnapshot();
        var store = new CatalogStore(new FakeCatalogLoader(expected));

        await store.ReloadAsync();

        Assert.Same(expected, store.Current);
    }

    [Fact]
    public async Task ReloadAsync_SecondLoad_ReplacesSnapshot()
    {
        var first = TestCatalog.CreateSnapshot();
        var second = TestCatalog.CreateSnapshot();
        var snapshots = new Queue<CatalogSnapshot>(new[] { first, second });
        var store = new CatalogStore(new FakeCatalogLoader(() => snapshots.Dequeue()));

        await store.ReloadAsync();
        await store.ReloadAsync();

        Assert.Same(second, store.Current);
    }

    [Fact]
    public async Task ReloadAsync_LoaderThrows_KeepsPreviousSnapshot()
    {
        var first = TestCatalog.CreateSnapshot();
        var shouldThrow = false;
        var store = new CatalogStore(new FakeCatalogLoader(
            () => shouldThrow ? throw new InvalidOperationException("boom") : first));

        await store.ReloadAsync();
        shouldThrow = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReloadAsync());

        Assert.Same(first, store.Current);
    }
}
