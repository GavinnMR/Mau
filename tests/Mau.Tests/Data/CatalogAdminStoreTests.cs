using Mau.Core.Admin;
using Mau.Core.Entities;
using Mau.Data;
using Mau.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Mau.Tests.Data;

public class CatalogAdminStoreTests
{
    [Fact]
    public async Task AddIngredientAsync_AddsRowAndSynonyms()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var suffix = NewSuffix();
        var name = $"z-ing-{suffix}";
        var firstSynonym = $"z-syn-a-{suffix}";
        var secondSynonym = $"z-syn-b-{suffix}";

        var id = await store.AddIngredientAsync(new IngredientInput(
            name, IngredientCategory.Other, false,
            new[]
            {
                new SynonymInput(firstSynonym, SynonymLanguage.Filipino),
                new SynonymInput(secondSynonym, SynonymLanguage.English),
            }));

        await using var context = await database.CreateContextAsync();
        var ingredient = await context.Ingredients
            .Include(candidate => candidate.Synonyms)
            .SingleAsync(candidate => candidate.Id == id);

        Assert.Equal(name, ingredient.CanonicalName);
        Assert.Equal(IngredientCategory.Other, ingredient.Category);
        Assert.False(ingredient.IsStaple);
        Assert.Equal(2, ingredient.Synonyms.Count);
        Assert.Contains(ingredient.Synonyms, synonym =>
            synonym.Name == firstSynonym && synonym.Language == SynonymLanguage.Filipino);
        Assert.Contains(ingredient.Synonyms, synonym =>
            synonym.Name == secondSynonym && synonym.Language == SynonymLanguage.English);
    }

    [Fact]
    public async Task AddIngredientAsync_DuplicateCanonicalNameIgnoringCase_ThrowsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.AddIngredientAsync(new IngredientInput(
                "CHICKEN", IngredientCategory.Protein, false, [])));

        Assert.Equal("That name is already in use.", exception.Message);
    }

    [Fact]
    public async Task AddIngredientAsync_DuplicateSynonymIgnoringCase_ThrowsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.AddIngredientAsync(new IngredientInput(
                $"z-ing-{NewSuffix()}", IngredientCategory.Other, false,
                new[] { new SynonymInput("TOYO", SynonymLanguage.Filipino) })));

        Assert.Equal("That name is already in use.", exception.Message);
    }

    [Fact]
    public async Task UpdateIngredientAsync_ChangesScalarsAndReplacesSynonyms()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var id = await GetIngredientIdAsync(database, "garlic");
        var newName = $"z-updated-{NewSuffix()}";
        var newSynonym = $"z-new-syn-{NewSuffix()}";

        await store.UpdateIngredientAsync(id, new IngredientInput(
            newName, IngredientCategory.Other, true,
            new[] { new SynonymInput(newSynonym, SynonymLanguage.English) }));

        await using var context = await database.CreateContextAsync();
        var ingredient = await context.Ingredients
            .Include(candidate => candidate.Synonyms)
            .SingleAsync(candidate => candidate.Id == id);

        Assert.Equal(newName, ingredient.CanonicalName);
        Assert.Equal(IngredientCategory.Other, ingredient.Category);
        Assert.True(ingredient.IsStaple);
        var synonym = Assert.Single(ingredient.Synonyms);
        Assert.Equal(newSynonym, synonym.Name);
        Assert.Equal(SynonymLanguage.English, synonym.Language);
    }

    [Fact]
    public async Task UpdateIngredientAsync_KeepingSameSynonyms_Succeeds()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var id = await GetIngredientIdAsync(database, "garlic");

        await store.UpdateIngredientAsync(id, new IngredientInput(
            "garlic", IngredientCategory.Aromatic, false,
            new[] { new SynonymInput("bawang", SynonymLanguage.Filipino) }));

        await using var context = await database.CreateContextAsync();
        var ingredient = await context.Ingredients
            .Include(candidate => candidate.Synonyms)
            .SingleAsync(candidate => candidate.Id == id);

        Assert.Equal("garlic", ingredient.CanonicalName);
        var synonym = Assert.Single(ingredient.Synonyms);
        Assert.Equal("bawang", synonym.Name);
    }

    [Fact]
    public async Task UpdateIngredientAsync_UnknownId_ThrowsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.UpdateIngredientAsync(999999, new IngredientInput(
                $"z-missing-{NewSuffix()}", IngredientCategory.Other, false, [])));

        Assert.Equal("Item not found.", exception.Message);
    }

    [Fact]
    public async Task UpdateIngredientAsync_DuplicateName_RollsBackNameAndSynonyms()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var id = await GetIngredientIdAsync(database, "garlic");
        var before = await GetIngredientSnapshotAsync(database, id);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.UpdateIngredientAsync(id, new IngredientInput(
                "ONION", IngredientCategory.Other, true,
                new[] { new SynonymInput($"z-should-not-stick-{NewSuffix()}", SynonymLanguage.Filipino) })));

        Assert.Equal("That name is already in use.", exception.Message);
        var after = await GetIngredientSnapshotAsync(database, id);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Category, after.Category);
        Assert.Equal(before.IsStaple, after.IsStaple);
        Assert.Equal(before.Synonyms, after.Synonyms);
    }

    [Fact]
    public async Task DeleteIngredientAsync_UnusedIngredient_CascadesSynonymsAndPantryRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var suffix = NewSuffix();
        var id = await store.AddIngredientAsync(new IngredientInput(
            $"z-del-{suffix}", IngredientCategory.Other, false,
            new[] { new SynonymInput($"z-del-syn-{suffix}", SynonymLanguage.Filipino) }));

        await database.CreateUserAsync("u1");
        await using (var context = await database.CreateContextAsync())
        {
            context.PantryItems.Add(new PantryItem { UserId = "u1", IngredientId = id });
            await context.SaveChangesAsync();
        }

        await store.DeleteIngredientAsync(id);

        await using var check = await database.CreateContextAsync();
        Assert.False(await check.Ingredients.AnyAsync(item => item.Id == id));
        Assert.False(await check.IngredientSynonyms.AnyAsync(item => item.IngredientId == id));
        Assert.False(await check.PantryItems.AnyAsync(item => item.IngredientId == id));
    }

    [Fact]
    public async Task DeleteIngredientAsync_UsedByDish_ThrowsConflictAndKeepsRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var id = await GetIngredientIdAsync(database, "chicken");

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.DeleteIngredientAsync(id));

        Assert.Equal("It is still in use and cannot be deleted.", exception.Message);
        await using var check = await database.CreateContextAsync();
        Assert.True(await check.Ingredients.AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task DeleteIngredientAsync_UnknownId_ThrowsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.DeleteIngredientAsync(999999));

        Assert.Equal("Item not found.", exception.Message);
    }

    [Fact]
    public async Task AddDishAsync_AddsRowsWithPositionsAndJoinedSteps()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var chickenId = await GetIngredientIdAsync(database, "chicken");
        var garlicId = await GetIngredientIdAsync(database, "garlic");
        var saltId = await GetIngredientIdAsync(database, "salt");

        var id = await store.AddDishAsync(new DishInput(
            $"z-dish-{NewSuffix()}", "Description.", CookingMethod.Soup, 30,
            new[] { "Boil the water.", "Add the shrimp." },
            new[]
            {
                new DishIngredientInput(chickenId, 1m, "kg", false),
                new DishIngredientInput(garlicId, null, null, false),
                new DishIngredientInput(saltId, null, null, true),
            }));

        await using var context = await database.CreateContextAsync();
        var dish = await context.Dishes
            .Include(candidate => candidate.Ingredients)
            .SingleAsync(candidate => candidate.Id == id);

        Assert.Equal("Boil the water.\nAdd the shrimp.", dish.Steps);
        var rows = dish.Ingredients.OrderBy(row => row.Position).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { 0, 1, 2 }, rows.Select(row => row.Position));
        Assert.Equal(chickenId, rows[0].IngredientId);
        Assert.Equal(1m, rows[0].Quantity);
        Assert.Equal("kg", rows[0].Unit);
        Assert.False(rows[0].IsOptional);
        Assert.Equal(garlicId, rows[1].IngredientId);
        Assert.Null(rows[1].Quantity);
        Assert.Null(rows[1].Unit);
        Assert.False(rows[1].IsOptional);
        Assert.Equal(saltId, rows[2].IngredientId);
        Assert.True(rows[2].IsOptional);
    }

    [Fact]
    public async Task AddDishAsync_DuplicateNameIgnoringCase_ThrowsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var chickenId = await GetIngredientIdAsync(database, "chicken");

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.AddDishAsync(new DishInput(
                "CHICKEN ADOBO", "Description.", CookingMethod.Soup, 30,
                new[] { "Step." },
                new[] { new DishIngredientInput(chickenId, 1m, "kg", false) })));

        Assert.Equal("That name is already in use.", exception.Message);
    }

    [Fact]
    public async Task UpdateDishAsync_ReplacesRowsAndKeepsFavorite()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var dishId = await GetDishIdAsync(database, "Chicken adobo");
        var porkId = await GetIngredientIdAsync(database, "pork");
        var onionId = await GetIngredientIdAsync(database, "onion");

        await database.CreateUserAsync("u1");
        await using (var context = await database.CreateContextAsync())
        {
            context.Favorites.Add(new Favorite
            {
                UserId = "u1",
                DishId = dishId,
                AddedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var newName = $"z-updated-dish-{NewSuffix()}";
        await store.UpdateDishAsync(dishId, new DishInput(
            newName, "New description.", CookingMethod.Fried, 20,
            new[] { "Fry." },
            new[]
            {
                new DishIngredientInput(porkId, 2m, "kg", false),
                new DishIngredientInput(onionId, null, null, true),
            }));

        await using var check = await database.CreateContextAsync();
        var dish = await check.Dishes
            .Include(candidate => candidate.Ingredients)
            .SingleAsync(candidate => candidate.Id == dishId);

        Assert.Equal(newName, dish.Name);
        var rows = dish.Ingredients.OrderBy(row => row.Position).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 0, 1 }, rows.Select(row => row.Position));
        Assert.Equal(new[] { porkId, onionId }, rows.Select(row => row.IngredientId));
        Assert.True(await check.Favorites.AnyAsync(favorite =>
            favorite.UserId == "u1" && favorite.DishId == dishId));
    }

    [Fact]
    public async Task UpdateDishAsync_UnknownId_ThrowsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var chickenId = await GetIngredientIdAsync(database, "chicken");

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.UpdateDishAsync(999999, new DishInput(
                $"z-missing-dish-{NewSuffix()}", "Description.", CookingMethod.Soup, 30,
                new[] { "Step." },
                new[] { new DishIngredientInput(chickenId, 1m, "kg", false) })));

        Assert.Equal("Item not found.", exception.Message);
    }

    [Fact]
    public async Task UpdateDishAsync_DuplicateName_RollsBackNameAndRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var dishId = await GetDishIdAsync(database, "Chicken adobo");
        var chickenId = await GetIngredientIdAsync(database, "chicken");
        var before = await GetDishSnapshotAsync(database, dishId);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.UpdateDishAsync(dishId, new DishInput(
                "Pork adobo", "Description.", CookingMethod.Soup, 30,
                new[] { "Step." },
                new[] { new DishIngredientInput(chickenId, 1m, "kg", false) })));

        Assert.Equal("That name is already in use.", exception.Message);
        var after = await GetDishSnapshotAsync(database, dishId);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Steps, after.Steps);
        Assert.Equal(before.Rows, after.Rows);
    }

    [Fact]
    public async Task DeleteDishAsync_RemovesDishRowsAndFavorites()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);
        var dishId = await GetDishIdAsync(database, "Pork adobo");

        await database.CreateUserAsync("u1");
        await using (var context = await database.CreateContextAsync())
        {
            context.Favorites.Add(new Favorite
            {
                UserId = "u1",
                DishId = dishId,
                AddedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        await store.DeleteDishAsync(dishId);

        await using var check = await database.CreateContextAsync();
        Assert.False(await check.Dishes.AnyAsync(dish => dish.Id == dishId));
        Assert.False(await check.DishIngredients.AnyAsync(row => row.DishId == dishId));
        Assert.False(await check.Favorites.AnyAsync(favorite => favorite.DishId == dishId));
    }

    [Fact]
    public async Task DeleteDishAsync_UnknownId_ThrowsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var store = new EfCatalogAdminStore(database.ContextFactory);

        var exception = await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.DeleteDishAsync(999999));

        Assert.Equal("Item not found.", exception.Message);
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<int> GetIngredientIdAsync(TestDatabase database, string name)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Ingredients
            .Where(ingredient => ingredient.CanonicalName == name)
            .Select(ingredient => ingredient.Id)
            .SingleAsync();
    }

    private static async Task<int> GetDishIdAsync(TestDatabase database, string name)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Dishes
            .Where(dish => dish.Name == name)
            .Select(dish => dish.Id)
            .SingleAsync();
    }

    private static async Task<(string Name, IngredientCategory Category, bool IsStaple, string[] Synonyms)>
        GetIngredientSnapshotAsync(TestDatabase database, int id)
    {
        await using var context = await database.CreateContextAsync();
        var ingredient = await context.Ingredients
            .Include(candidate => candidate.Synonyms)
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == id);

        return (
            ingredient.CanonicalName,
            ingredient.Category,
            ingredient.IsStaple,
            ingredient.Synonyms
                .Select(synonym => synonym.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static async Task<(string Name, string Steps, DishRowSnapshot[] Rows)>
        GetDishSnapshotAsync(TestDatabase database, int dishId)
    {
        await using var context = await database.CreateContextAsync();
        var dish = await context.Dishes
            .Include(candidate => candidate.Ingredients)
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == dishId);

        return (
            dish.Name,
            dish.Steps,
            dish.Ingredients
                .OrderBy(row => row.Position)
                .Select(row => new DishRowSnapshot(
                    row.Position, row.IngredientId, row.Quantity, row.Unit, row.IsOptional))
                .ToArray());
    }

    private sealed record DishRowSnapshot(
        int Position, int IngredientId, decimal? Quantity, string? Unit, bool IsOptional);
}
