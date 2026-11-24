using Mau.Core.Entities;
using Mau.Core.Users;
using Mau.Data;
using Mau.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Mau.Tests.Data;

public class UserStoresTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pantry_ReplaceThenGet_ReturnsSavedIds()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPantryStore(database.ContextFactory);
        var ids = await database.GetIngredientIdsAsync(2);

        await store.ReplaceAsync("u1", ids);

        Assert.Equal(ids, (await store.GetIngredientIdsAsync("u1")).OrderBy(id => id));
    }

    [Fact]
    public async Task Pantry_SecondReplace_ReplacesPreviousIds()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPantryStore(database.ContextFactory);
        var ids = await database.GetIngredientIdsAsync(3);

        await store.ReplaceAsync("u1", new[] { ids[0], ids[1] });
        await store.ReplaceAsync("u1", new[] { ids[2] });

        Assert.Equal(
            new[] { ids[2] },
            (await store.GetIngredientIdsAsync("u1")).OrderBy(id => id));
    }

    [Fact]
    public async Task Pantry_ReplaceWithEmpty_Clears()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPantryStore(database.ContextFactory);
        var ids = await database.GetIngredientIdsAsync(2);
        await store.ReplaceAsync("u1", ids);

        await store.ReplaceAsync("u1", Array.Empty<int>());

        Assert.Empty(await store.GetIngredientIdsAsync("u1"));
    }

    [Fact]
    public async Task Pantry_TwoUsers_AreIsolated()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        await database.CreateUserAsync("u2");
        var store = new EfPantryStore(database.ContextFactory);
        var ids = await database.GetIngredientIdsAsync(2);

        await store.ReplaceAsync("u1", new[] { ids[0] });
        await store.ReplaceAsync("u2", new[] { ids[1] });

        Assert.Equal(
            new[] { ids[0] },
            (await store.GetIngredientIdsAsync("u1")).OrderBy(id => id));
        Assert.Equal(
            new[] { ids[1] },
            (await store.GetIngredientIdsAsync("u2")).OrderBy(id => id));
    }

    [Fact]
    public async Task Pantry_DeletingIngredient_CascadesPantryRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfPantryStore(database.ContextFactory);

        int ingredientId;
        await using (var context = await database.CreateContextAsync())
        {
            var ingredient = new Ingredient
            {
                CanonicalName = "test pantry ingredient",
                Category = IngredientCategory.Other,
            };
            context.Ingredients.Add(ingredient);
            await context.SaveChangesAsync();
            ingredientId = ingredient.Id;
        }

        await store.ReplaceAsync("u1", new[] { ingredientId });

        await using (var context = await database.CreateContextAsync())
        {
            var ingredient = await context.Ingredients
                .SingleAsync(candidate => candidate.Id == ingredientId);
            context.Ingredients.Remove(ingredient);
            await context.SaveChangesAsync();
        }

        Assert.Empty(await store.GetIngredientIdsAsync("u1"));
    }

    [Fact]
    public async Task Favorites_AddTwice_LeavesOneRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfFavoriteStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var dishId = (await database.GetDishIdsAsync(1))[0];

        await store.AddAsync("u1", dishId);
        await store.AddAsync("u1", dishId);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.Set<Favorite>().CountAsync());
    }

    [Fact]
    public async Task Favorites_GetDishIds_ReturnsNewestFirst()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var time = new FixedTimeProvider(StartTime);
        var store = new EfFavoriteStore(database.ContextFactory, time);
        var dishIds = await database.GetDishIdsAsync(2);

        await store.AddAsync("u1", dishIds[0]);
        time.Advance(TimeSpan.FromMinutes(1));
        await store.AddAsync("u1", dishIds[1]);

        Assert.Equal(new[] { dishIds[1], dishIds[0] }, await store.GetDishIdsAsync("u1"));
    }

    [Fact]
    public async Task Favorites_ExistsAsync_TrueAndFalse()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfFavoriteStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.AddAsync("u1", dishId);

        Assert.True(await store.ExistsAsync("u1", dishId));
        Assert.False(await store.ExistsAsync("u1", 99999));
    }

    [Fact]
    public async Task Favorites_RemoveAbsent_DoesNotThrow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfFavoriteStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var dishId = (await database.GetDishIdsAsync(1))[0];

        await store.RemoveAsync("u1", dishId);

        Assert.False(await store.ExistsAsync("u1", dishId));
    }

    [Fact]
    public async Task Favorites_RemovePresent_Removes()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfFavoriteStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.AddAsync("u1", dishId);

        await store.RemoveAsync("u1", dishId);

        Assert.False(await store.ExistsAsync("u1", dishId));
    }

    [Fact]
    public async Task Favorites_DeletingDish_CascadesFavorites()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfFavoriteStore(database.ContextFactory, new FixedTimeProvider(StartTime));

        int dishId;
        await using (var context = await database.CreateContextAsync())
        {
            var dish = new Dish
            {
                Name = "Test favorite dish",
                Description = "A test dish.",
                CookingMethod = CookingMethod.Fried,
                CookTimeMinutes = 5,
                Steps = "Cook.",
            };
            context.Dishes.Add(dish);
            await context.SaveChangesAsync();
            dishId = dish.Id;
        }

        await store.AddAsync("u1", dishId);

        await using (var context = await database.CreateContextAsync())
        {
            var dish = await context.Dishes.SingleAsync(candidate => candidate.Id == dishId);
            context.Dishes.Remove(dish);
            await context.SaveChangesAsync();
        }

        Assert.Empty(await store.GetDishIdsAsync("u1"));
    }

    [Fact]
    public async Task SearchLog_AddThenGetRecent_ReturnsNewestFirstAndRespectsMax()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var time = new FixedTimeProvider(StartTime);
        var store = new EfSearchLogStore(database.ContextFactory, time);
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];

        for (var i = 0; i < 3; i++)
        {
            await store.AddAsync("u1", new[] { ingredientId }, i);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        var recent = await store.GetRecentAsync("u1", 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal(2, recent[0].ResultCount);
        Assert.Equal(1, recent[1].ResultCount);
    }

    [Fact]
    public async Task SearchLog_GetRecent_ReturnsIngredientIdsAscending()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfSearchLogStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var ids = await database.GetIngredientIdsAsync(2);

        await store.AddAsync("u1", new[] { ids[1], ids[0] }, 1);

        var recent = await store.GetRecentAsync("u1", 10);
        Assert.Equal(ids, recent[0].IngredientIds);
    }

    [Fact]
    public async Task SearchLog_GuestLog_NeverAppearsInUserList()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfSearchLogStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];

        await store.AddAsync(null, new[] { ingredientId }, 1);
        await store.AddAsync("u1", new[] { ingredientId }, 1);

        Assert.Single(await store.GetRecentAsync("u1", 10));
    }

    [Fact]
    public async Task SearchLog_Delete_OnlyRemovesOwnRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        await database.CreateUserAsync("u2");
        var store = new EfSearchLogStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];
        await store.AddAsync("u1", new[] { ingredientId }, 1);
        var logId = (await store.GetRecentAsync("u1", 10))[0].Id;

        Assert.False(await store.DeleteAsync("u2", logId));
        Assert.Single(await store.GetRecentAsync("u1", 10));

        Assert.True(await store.DeleteAsync("u1", logId));
        Assert.Empty(await store.GetRecentAsync("u1", 10));
    }

    [Fact]
    public async Task SearchLog_DeleteAll_ReturnsCountAndKeepsOtherUsers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        await database.CreateUserAsync("u2");
        var store = new EfSearchLogStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];

        await store.AddAsync("u1", new[] { ingredientId }, 1);
        await store.AddAsync("u1", new[] { ingredientId }, 1);
        await store.AddAsync("u2", new[] { ingredientId }, 1);

        Assert.Equal(2, await store.DeleteAllAsync("u1"));
        Assert.Empty(await store.GetRecentAsync("u1", 10));
        Assert.Single(await store.GetRecentAsync("u2", 10));
    }

    [Fact]
    public async Task SearchLog_Trim_KeepsNewestTwo()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var time = new FixedTimeProvider(StartTime);
        var store = new EfSearchLogStore(database.ContextFactory, time);
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];

        for (var i = 0; i < 4; i++)
        {
            await store.AddAsync("u1", new[] { ingredientId }, i);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        await store.TrimAsync("u1", 2);

        var recent = await store.GetRecentAsync("u1", 10);
        Assert.Equal(2, recent.Count);
        Assert.Equal(3, recent[0].ResultCount);
        Assert.Equal(2, recent[1].ResultCount);
    }

    [Fact]
    public async Task SearchLog_DeletingUser_SetsUserIdToNullAndKeepsRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = new EfSearchLogStore(database.ContextFactory, new FixedTimeProvider(StartTime));
        var ingredientId = (await database.GetIngredientIdsAsync(1))[0];
        await store.AddAsync("u1", new[] { ingredientId }, 1);

        await using (var context = await database.CreateContextAsync())
        {
            var user = await context.Users.SingleAsync(candidate => candidate.Id == "u1");
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }

        await using var check = await database.CreateContextAsync();
        var log = await check.Set<SearchLog>().SingleAsync();
        Assert.Null(log.UserId);
    }
}
