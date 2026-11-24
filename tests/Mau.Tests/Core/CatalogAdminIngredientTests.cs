using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class CatalogAdminIngredientTests
{
    [Fact]
    public void Snapshot_BayLeafSynonymItems_AreSortedWithLanguages()
    {
        var snapshot = TestCatalog.CreateSnapshot();
        var bayLeaf = snapshot.IngredientsById[7];

        Assert.Equal(
            new[]
            {
                new CatalogSynonym("dahon ng laurel", SynonymLanguage.Filipino),
                new CatalogSynonym("laurel", SynonymLanguage.Filipino),
            },
            bayLeaf.SynonymItems);
        Assert.Equal(
            bayLeaf.Synonyms.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            bayLeaf.SynonymItems.Select(synonym => synonym.Name));
    }

    [Fact]
    public async Task Create_ValidIngredient_AddsAndReloads()
    {
        var (service, store, loader) = await CreateAsync();
        var input = new IngredientInput(
            "dragonfruit", IngredientCategory.Other, false,
            new[] { new SynonymInput("pitaya", SynonymLanguage.Filipino) });

        var result = await service.CreateIngredientAsync(input);

        Assert.True(result.Succeeded);
        Assert.Equal(101, result.Id);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.AddIngredientAsync), call.Method);
        Assert.NotNull(call.IngredientInput);
        Assert.Equal("dragonfruit", call.IngredientInput!.Name);
        Assert.Equal("pitaya", Assert.Single(call.IngredientInput.Synonyms).Name);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Create_NameIsCleanedBeforeStore()
    {
        var (service, store, loader) = await CreateAsync();

        await service.CreateIngredientAsync(
            new IngredientInput("  Dragon   fruit  ", IngredientCategory.Other, false, []));

        var call = Assert.Single(store.Calls);
        Assert.Equal("Dragon fruit", call.IngredientInput!.Name);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Create_BlankSynonymsAreIgnored()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[]
                {
                    new SynonymInput("", SynonymLanguage.Filipino),
                    new SynonymInput("  ", SynonymLanguage.Filipino),
                    new SynonymInput(null, SynonymLanguage.Filipino),
                    new SynonymInput("pitaya", SynonymLanguage.Filipino),
                }));

        Assert.True(result.Succeeded);
        var call = Assert.Single(store.Calls);
        var synonym = Assert.Single(call.IngredientInput!.Synonyms);
        Assert.Equal("pitaya", synonym.Name);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Create_EmptyName_FailsWithoutStoreOrReload()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput("  ", IngredientCategory.Other, false, []));

        AssertError(result, "Name", "Name is required.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_NameLongerThanHundred_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(new string('a', 101), IngredientCategory.Other, false, []));

        AssertError(result, "Name", "Name must be at most 100 characters.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_CommaInNameOrSynonym_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var nameResult = await service.CreateIngredientAsync(
            new IngredientInput("a,b", IngredientCategory.Other, false, []));
        AssertError(nameResult, "Name", "Name cannot contain a comma.");

        var synonymResult = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[] { new SynonymInput("a,b", SynonymLanguage.Filipino) }));
        AssertError(
            synonymResult, "Synonyms[0].Name", "Synonym cannot contain a comma.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_UnknownCategoryOrLanguage_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var categoryResult = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", (IngredientCategory)99, false, []));
        AssertError(categoryResult, "Category", "Choose a category.");

        var languageResult = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[] { new SynonymInput("pitaya", (SynonymLanguage)99) }));
        AssertError(
            languageResult, "Synonyms[0].Language", "Choose a language.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_TooManySynonymsOrTooLongSynonym_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var many = Enumerable.Range(0, 21)
            .Select(index => new SynonymInput($"s{index}", SynonymLanguage.Filipino))
            .ToArray();
        var manyResult = await service.CreateIngredientAsync(
            new IngredientInput("dragonfruit", IngredientCategory.Other, false, many));
        AssertError(manyResult, "Synonyms", "At most 20 synonyms.");

        var longResult = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[] { new SynonymInput(new string('a', 101), SynonymLanguage.Filipino) }));
        AssertError(
            longResult, "Synonyms[0].Name", "Synonym must be at most 100 characters.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_NameMatchesExistingCanonicalIgnoringCase_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput("CHICKEN", IngredientCategory.Other, false, []));

        AssertError(result, "Name", "That name is already used by chicken.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_NameMatchesExistingSynonym_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput("toyo", IngredientCategory.Other, false, []));

        AssertError(result, "Name", "That name is already used by soy sauce.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_SynonymMatchesExistingIngredient_FailsWithIndex()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[]
                {
                    new SynonymInput("pitaya", SynonymLanguage.Filipino),
                    new SynonymInput("pork", SynonymLanguage.Filipino),
                }));

        AssertError(
            result, "Synonyms[1].Name", "“pork” is already used by pork.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_SynonymMatchesOwnName_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[] { new SynonymInput("Dragonfruit", SynonymLanguage.Filipino) }));

        AssertError(
            result, "Synonyms[0].Name", "“Dragonfruit” is listed more than once.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_DuplicateSynonym_ReportsLaterIndex()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[]
                {
                    new SynonymInput("pitaya", SynonymLanguage.Filipino),
                    new SynonymInput("x", SynonymLanguage.Filipino),
                    new SynonymInput("PITAYA", SynonymLanguage.Filipino),
                }));

        AssertError(
            result, "Synonyms[2].Name", "“PITAYA” is listed more than once.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_BlankRowsCountForTheSubmittedIndex()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput(
                "dragonfruit", IngredientCategory.Other, false,
                new[]
                {
                    new SynonymInput("", SynonymLanguage.Filipino),
                    new SynonymInput("pork", SynonymLanguage.Filipino),
                }));

        AssertError(
            result, "Synonyms[1].Name", "“pork” is already used by pork.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_SeveralProblems_ReturnsAllErrors()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateIngredientAsync(
            new IngredientInput("", (IngredientCategory)99, false, []));

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors, error => error.Field == "Name" && error.Message == "Name is required.");
        Assert.Contains(
            result.Errors,
            error => error.Field == "Category" && error.Message == "Choose a category.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_StoreThrowsConflict_FailsWithoutReload()
    {
        var (service, store, loader) = await CreateAsync();
        store.ExceptionToThrow = new CatalogConflictException("That name is already in use.");

        var result = await service.CreateIngredientAsync(
            new IngredientInput("dragonfruit", IngredientCategory.Other, false, []));

        AssertError(result, "Name", "That name is already in use.");
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Update_UnknownId_FailsWithoutStoreCall()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateIngredientAsync(
            999, new IngredientInput("dragonfruit", IngredientCategory.Other, false, []));

        AssertError(result, "", "Ingredient not found.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Update_KeepingOwnNameAndSynonym_Succeeds()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateIngredientAsync(
            1,
            new IngredientInput(
                "chicken", IngredientCategory.Protein, false,
                new[] { new SynonymInput("manok", SynonymLanguage.Filipino) }));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Id);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.UpdateIngredientAsync), call.Method);
        Assert.Equal(1, call.Id);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Update_ToAnotherIngredientsName_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateIngredientAsync(
            1,
            new IngredientInput(
                "pork", IngredientCategory.Protein, false,
                new[] { new SynonymInput("manok", SynonymLanguage.Filipino) }));

        AssertError(result, "Name", "That name is already used by pork.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_UnknownId_FailsWithoutStoreCall()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.DeleteIngredientAsync(999);

        AssertError(result, "", "Ingredient not found.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_IngredientUsedByThreeDishes_ListsAllNames()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.DeleteIngredientAsync(1);

        AssertError(
            result,
            "",
            "Cannot delete “chicken”: it is used by 3 dishes: " +
            "Chicken adobo, Chicken tinola, Garlic fried chicken.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_IngredientUsedByOneDish_UsesSingular()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.DeleteIngredientAsync(10);

        AssertError(
            result, "",
            "Cannot delete “ginger”: it is used by 1 dish: Chicken tinola.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_IngredientUsedBySevenDishes_ListsFirstFiveAndMore()
    {
        var (service, store, loader) = await CreateAsync(CreateSevenDishSnapshot());

        var result = await service.DeleteIngredientAsync(1);

        AssertError(
            result, "",
            "Cannot delete “chicken”: it is used by 7 dishes: " +
            "A, B, C, D, E and 2 more.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_UnusedIngredient_DeletesAndReloads()
    {
        var (service, store, loader) = await CreateAsync(CreateUnusedIngredientSnapshot());

        var result = await service.DeleteIngredientAsync(99);

        Assert.True(result.Succeeded);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.DeleteIngredientAsync), call.Method);
        Assert.Equal(99, call.Id);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Delete_StoreThrowsConflict_FailsWithoutReload()
    {
        var (service, store, loader) = await CreateAsync(CreateUnusedIngredientSnapshot());
        store.ExceptionToThrow =
            new CatalogConflictException("It is still in use and cannot be deleted.");

        var result = await service.DeleteIngredientAsync(99);

        AssertError(result, "", "It is still in use and cannot be deleted.");
        AssertNoReload(loader);
    }

    private static async Task<TestContext> CreateAsync(CatalogSnapshot? snapshot = null)
    {
        var loader = new CountingCatalogLoader(snapshot ?? TestCatalog.CreateSnapshot());
        var catalog = new CatalogStore(loader);
        await catalog.ReloadAsync();
        var store = new FakeCatalogAdminStore();

        return new TestContext(new CatalogAdminService(store, catalog), store, loader);
    }

    private static void AssertError(AdminResult result, string field, string message)
    {
        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors, error => error.Field == field && error.Message == message);
    }

    private static void AssertNoReload(CountingCatalogLoader loader) =>
        Assert.Equal(1, loader.LoadCount);

    private static CatalogSnapshot CreateSevenDishSnapshot()
    {
        var chicken = new Ingredient
        {
            Id = 1,
            CanonicalName = "chicken",
            Category = IngredientCategory.Protein,
        };

        var dishes = Enumerable.Range(0, 7)
            .Select(index => new Dish
            {
                Id = index + 1,
                Name = ((char)('A' + index)).ToString(),
                Description = "Description",
                CookingMethod = CookingMethod.Fried,
                CookTimeMinutes = 10,
                Steps = "Cook.",
                Ingredients = new List<DishIngredient>
                {
                    new()
                    {
                        DishId = index + 1,
                        IngredientId = chicken.Id,
                        Ingredient = chicken,
                        Position = 0,
                    },
                },
            })
            .ToList();

        return CatalogSnapshot.Create(new[] { chicken }, dishes);
    }

    private static CatalogSnapshot CreateUnusedIngredientSnapshot()
    {
        var ingredient = new Ingredient
        {
            Id = 99,
            CanonicalName = "dragonfruit",
            Category = IngredientCategory.Other,
        };

        return CatalogSnapshot.Create(new[] { ingredient }, Array.Empty<Dish>());
    }

    private sealed record TestContext(
        CatalogAdminService Service,
        FakeCatalogAdminStore Store,
        CountingCatalogLoader Loader);
}
