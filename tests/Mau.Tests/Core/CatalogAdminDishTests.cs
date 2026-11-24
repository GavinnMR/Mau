using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class CatalogAdminDishTests
{
    [Fact]
    public async Task Create_ValidDish_AddsAndReloads()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateDishAsync(ValidDish());

        Assert.True(result.Succeeded);
        Assert.Equal(101, result.Id);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.AddDishAsync), call.Method);
        var input = call.DishInput!;
        Assert.Equal("Sinigang na hipon", input.Name);
        Assert.Equal(3, input.Ingredients.Count);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Create_CleansTextStepsUnitsAndDropsBlankRows()
    {
        var (service, store, loader) = await CreateAsync();
        var input = new DishInput(
            "  Sinigang   na  hipon ",
            "  Shrimp   in tamarind soup.  ",
            CookingMethod.Soup,
            30,
            new[] { "  Boil the water.  ", "", "   ", "Add the shrimp." },
            new[]
            {
                new DishIngredientInput(null, 2m, "  ", false),
                new DishIngredientInput(1, 1m, " kg ", false),
                new DishIngredientInput(5, null, "  ", false),
                new DishIngredientInput(8, null, null, true),
            });

        var result = await service.CreateDishAsync(input);

        Assert.True(result.Succeeded);
        var stored = Assert.Single(store.Calls).DishInput!;
        Assert.Equal("Sinigang na hipon", stored.Name);
        Assert.Equal("Shrimp in tamarind soup.", stored.Description);
        Assert.Equal(new[] { "Boil the water.", "Add the shrimp." }, stored.Steps);
        Assert.Equal(
            new[] { 1, 5, 8 },
            stored.Ingredients.Select(row => row.IngredientId!.Value));
        Assert.Equal("kg", stored.Ingredients[0].Unit);
        Assert.Null(stored.Ingredients[1].Unit);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Create_EmptyOrTooLongName_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var empty = await service.CreateDishAsync(ValidDish() with { Name = "  " });
        AssertError(empty, "Name", "Name is required.");

        var tooLong = await service.CreateDishAsync(
            ValidDish() with { Name = new string('a', 121) });
        AssertError(tooLong, "Name", "Name must be at most 120 characters.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_ExistingNameIgnoringCase_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateDishAsync(
            ValidDish() with { Name = "chicken ADOBO" });

        AssertError(result, "Name", "A dish with that name already exists.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_EmptyOrTooLongDescription_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var empty = await service.CreateDishAsync(
            ValidDish() with { Description = "  " });
        AssertError(empty, "Description", "Description is required.");

        var tooLong = await service.CreateDishAsync(
            ValidDish() with { Description = new string('a', 501) });
        AssertError(tooLong, "Description", "Description must be at most 500 characters.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_UnknownCookingMethod_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.CreateDishAsync(
            ValidDish() with { CookingMethod = (CookingMethod)99 });

        AssertError(result, "CookingMethod", "Choose a cooking method.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_CookTimeOutsideRange_FailsAndEdgesSucceed()
    {
        var (service, store, loader) = await CreateAsync();

        var zero = await service.CreateDishAsync(
            ValidDish() with { CookTimeMinutes = 0 });
        AssertError(
            zero, "CookTimeMinutes",
            "Cooking time must be between 1 and 600 minutes.");

        var tooLong = await service.CreateDishAsync(
            ValidDish() with { CookTimeMinutes = 601 });
        AssertError(
            tooLong, "CookTimeMinutes",
            "Cooking time must be between 1 and 600 minutes.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);

        Assert.True((await service.CreateDishAsync(
            ValidDish() with { CookTimeMinutes = 1 })).Succeeded);
        Assert.True((await service.CreateDishAsync(
            ValidDish() with { CookTimeMinutes = 600 })).Succeeded);
    }

    [Fact]
    public async Task Create_EmptyTooLongOrTooManySteps_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var noSteps = await service.CreateDishAsync(
            ValidDish() with { Steps = Array.Empty<string>() });
        AssertError(noSteps, "Steps", "Add at least one step.");

        var blankSteps = await service.CreateDishAsync(
            ValidDish() with { Steps = new[] { "", "   " } });
        AssertError(blankSteps, "Steps", "Add at least one step.");

        var longStep = await service.CreateDishAsync(
            ValidDish() with { Steps = new[] { new string('a', 501) } });
        AssertError(
            longStep, "Steps", "Each step must be at most 500 characters.");

        var manySteps = await service.CreateDishAsync(
            ValidDish() with
            {
                Steps = Enumerable.Range(0, 9).Select(_ => new string('a', 480)).ToArray(),
            });
        AssertError(
            manySteps, "Steps",
            "Steps are too long (4000 characters in total).");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_NoRequiredIngredient_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var noRows = await service.CreateDishAsync(
            ValidDish() with { Ingredients = Array.Empty<DishIngredientInput>() });
        AssertError(
            noRows, "Ingredients",
            "A dish needs at least one required ingredient.");

        var optionalOnly = await service.CreateDishAsync(
            ValidDish() with
            {
                Ingredients = new[]
                {
                    new DishIngredientInput(1, 1m, "kg", true),
                },
            });
        AssertError(
            optionalOnly, "Ingredients",
            "A dish needs at least one required ingredient.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_MoreThanFortyChosenIngredients_Fails()
    {
        var (service, store, loader) = await CreateAsync();
        var rows = Enumerable.Range(1, 41)
            .Select(id => new DishIngredientInput(id, null, null, false))
            .ToArray();

        var result = await service.CreateDishAsync(
            ValidDish() with { Ingredients = rows });

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Message == "A dish can have at most 40 ingredients.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_UnknownIngredient_ReportsSubmittedIndex()
    {
        var (service, store, loader) = await CreateAsync();
        var rows = new[]
        {
            new DishIngredientInput(1, 1m, "kg", false),
            new DishIngredientInput(999, null, null, false),
            new DishIngredientInput(5, null, null, false),
        };

        var result = await service.CreateDishAsync(
            ValidDish() with { Ingredients = rows });

        AssertError(
            result, "Ingredients[1].IngredientId", "Choose an existing ingredient.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_DuplicateIngredient_ReportsLaterIndex()
    {
        var (service, store, loader) = await CreateAsync();
        var rows = new[]
        {
            new DishIngredientInput(1, 1m, "kg", false),
            new DishIngredientInput(5, null, null, false),
            new DishIngredientInput(1, null, null, false),
        };

        var result = await service.CreateDishAsync(
            ValidDish() with { Ingredients = rows });

        AssertError(
            result, "Ingredients[2].IngredientId",
            "chicken is listed more than once.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_QuantityRules_AndAcceptedValues()
    {
        var (service, store, loader) = await CreateAsync();

        foreach (var quantity in new[] { 0m, -1m })
        {
            var result = await service.CreateDishAsync(
                SingleRowDish(new DishIngredientInput(1, quantity, null, false)));
            AssertError(
                result, "Ingredients[0].Quantity",
                "Quantity must be greater than 0.");
        }

        var tooHigh = await service.CreateDishAsync(
            SingleRowDish(new DishIngredientInput(1, 100001m, null, false)));
        AssertError(
            tooHigh, "Ingredients[0].Quantity",
            "Quantity must be at most 100000.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);

        Assert.True((await service.CreateDishAsync(
            SingleRowDish(new DishIngredientInput(1, 100000m, "kg", false)))).Succeeded);
        Assert.True((await service.CreateDishAsync(
            SingleRowDish(new DishIngredientInput(1, null, null, false)))).Succeeded);
    }

    [Fact]
    public async Task Create_UnitRules_Fail()
    {
        var (service, store, loader) = await CreateAsync();

        var tooLong = await service.CreateDishAsync(
            SingleRowDish(new DishIngredientInput(1, 1m, new string('u', 21), false)));
        AssertError(
            tooLong, "Ingredients[0].Unit", "Unit must be at most 20 characters.");

        var unitWithoutQuantity = await service.CreateDishAsync(
            SingleRowDish(new DishIngredientInput(1, null, "kg", false)));
        AssertError(
            unitWithoutQuantity, "Ingredients[0].Unit",
            "Enter a quantity or clear the unit.");

        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_BlankRowWithQuantity_IsIgnoredButIndexCounts()
    {
        var (service, store, loader) = await CreateAsync();
        var rows = new[]
        {
            new DishIngredientInput(null, 5m, "kg", false),
            new DishIngredientInput(999, null, null, false),
        };

        var result = await service.CreateDishAsync(
            ValidDish() with { Ingredients = rows });

        AssertError(
            result, "Ingredients[1].IngredientId", "Choose an existing ingredient.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_SeveralProblems_ReturnsAllErrors()
    {
        var (service, store, loader) = await CreateAsync();
        var input = ValidDish() with
        {
            Name = "",
            CookingMethod = (CookingMethod)99,
            Steps = Array.Empty<string>(),
        };

        var result = await service.CreateDishAsync(input);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors, error => error.Field == "Name" && error.Message == "Name is required.");
        Assert.Contains(
            result.Errors,
            error => error.Field == "CookingMethod" &&
                error.Message == "Choose a cooking method.");
        Assert.Contains(
            result.Errors, error => error.Field == "Steps" && error.Message == "Add at least one step.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Create_StoreThrowsConflict_FailsWithoutReload()
    {
        var (service, store, loader) = await CreateAsync();
        store.ExceptionToThrow = new CatalogConflictException("That name is already in use.");

        var result = await service.CreateDishAsync(ValidDish());

        AssertError(result, "Name", "That name is already in use.");
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Update_UnknownId_FailsWithoutStoreCall()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateDishAsync(999, ValidDish());

        AssertError(result, "", "Dish not found.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Update_KeepingOwnName_Succeeds()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateDishAsync(
            1, ValidDish() with { Name = "Chicken adobo" });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Id);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.UpdateDishAsync), call.Method);
        Assert.Equal(1, call.Id);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Update_ToAnotherDishName_Fails()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.UpdateDishAsync(
            1, ValidDish() with { Name = "Pork adobo" });

        AssertError(result, "Name", "A dish with that name already exists.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_UnknownId_FailsWithoutStoreCall()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.DeleteDishAsync(999);

        AssertError(result, "", "Dish not found.");
        Assert.Empty(store.Calls);
        AssertNoReload(loader);
    }

    [Fact]
    public async Task Delete_ExistingDish_DeletesAndReloads()
    {
        var (service, store, loader) = await CreateAsync();

        var result = await service.DeleteDishAsync(2);

        Assert.True(result.Succeeded);
        var call = Assert.Single(store.Calls);
        Assert.Equal(nameof(FakeCatalogAdminStore.DeleteDishAsync), call.Method);
        Assert.Equal(2, call.Id);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task Delete_StoreThrowsConflict_FailsWithoutReload()
    {
        var (service, store, loader) = await CreateAsync();
        store.ExceptionToThrow = new CatalogConflictException("Item not found.");

        var result = await service.DeleteDishAsync(2);

        AssertError(result, "", "Item not found.");
        AssertNoReload(loader);
    }

    private static DishInput ValidDish() => new(
        "Sinigang na hipon",
        "Shrimp in tamarind soup.",
        CookingMethod.Soup,
        30,
        new[] { "Boil the water.", "Add the shrimp." },
        new[]
        {
            new DishIngredientInput(1, 1m, "kg", false),
            new DishIngredientInput(5, null, null, false),
            new DishIngredientInput(8, null, null, true),
        });

    private static DishInput SingleRowDish(DishIngredientInput row) =>
        ValidDish() with { Ingredients = new[] { row } };

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

    private sealed record TestContext(
        CatalogAdminService Service,
        FakeCatalogAdminStore Store,
        CountingCatalogLoader Loader);
}
