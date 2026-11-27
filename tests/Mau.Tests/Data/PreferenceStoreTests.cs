using Mau.Core.Entities;
using Mau.Core.Users;
using Mau.Data;
using Mau.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Mau.Tests.Data;

public class PreferenceStoreTests
{
    [Fact]
    public async Task Get_NoRow_ReturnsNone()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");

        Assert.Equal(UserPreferences.None, await new EfPreferenceStore(database.ContextFactory).GetAsync("u1"));
    }

    [Fact]
    public async Task Save_ThenGet_RoundTrips_AndSecondSaveUpdates()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPreferenceStore(database.ContextFactory);
        var proteinId = await ProteinIdAsync(database, "pork");

        await store.SaveAsync("u1", new UserPreferences(proteinId, CookingMethod.Soup, 45));
        await store.SaveAsync("u1", new UserPreferences(proteinId, CookingMethod.Fried, null));

        Assert.Equal(
            new UserPreferences(proteinId, CookingMethod.Fried, null),
            await store.GetAsync("u1"));
        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.UserPreferences.CountAsync());
    }

    [Fact]
    public async Task SaveEmpty_RemovesRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPreferenceStore(database.ContextFactory);
        await store.SaveAsync("u1", new UserPreferences(null, CookingMethod.Soup, null));

        await store.SaveAsync("u1", UserPreferences.None);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(0, await context.UserPreferences.CountAsync());
    }

    [Fact]
    public async Task DeletingUser_CascadesPreferences()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        await new EfPreferenceStore(database.ContextFactory)
            .SaveAsync("u1", new UserPreferences(null, null, 30));

        await using (var context = await database.CreateContextAsync())
        {
            context.Users.Remove(await context.Users.SingleAsync(user => user.Id == "u1"));
            await context.SaveChangesAsync();
        }

        await using var check = await database.CreateContextAsync();
        Assert.Equal(0, await check.UserPreferences.CountAsync());
    }

    [Fact]
    public async Task DeletingPreferredProtein_ClearsOnlyTheProtein()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        int quailId;
        await using (var context = await database.CreateContextAsync())
        {
            var quail = new Ingredient { CanonicalName = "quail", Category = IngredientCategory.Protein };
            context.Ingredients.Add(quail);
            await context.SaveChangesAsync();
            quailId = quail.Id;
        }

        var store = new EfPreferenceStore(database.ContextFactory);
        await store.SaveAsync("u1", new UserPreferences(quailId, CookingMethod.Grilled, null));

        await using (var context = await database.CreateContextAsync())
        {
            context.Ingredients.Remove(await context.Ingredients.SingleAsync(item => item.Id == quailId));
            await context.SaveChangesAsync();
        }

        Assert.Equal(new UserPreferences(null, CookingMethod.Grilled, null), await store.GetAsync("u1"));
    }

    private static async Task<int> ProteinIdAsync(TestDatabase database, string name)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Ingredients
            .Where(ingredient => ingredient.CanonicalName == name)
            .Select(ingredient => ingredient.Id)
            .SingleAsync();
    }
}
