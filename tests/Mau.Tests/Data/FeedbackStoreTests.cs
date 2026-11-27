using Mau.Core.Entities;
using Mau.Core.Feedback;
using Mau.Data;
using Mau.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Mau.Tests.Data;

public class FeedbackStoreTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static EfFeedbackStore CreateStore(TestDatabase database) =>
        new(database.ContextFactory, new FixedTimeProvider(StartTime));

    private static string NewKey() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task AddCooked_SavesRowAndCountsIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];

        var totals = await store.AddCookedAsync("u1", dishId, NewKey());

        Assert.Equal(new DishTotals(1, 0, 0), totals);
        await using var context = await database.CreateContextAsync();
        var row = await context.DishFeedback.SingleAsync();
        Assert.Equal(("u1", dishId, FeedbackType.Cooked), (row.UserId, row.DishId, row.Type));
        Assert.Equal(StartTime.UtcDateTime, row.CreatedAt);
    }

    [Fact]
    public async Task AddCooked_TwoSearches_CountsBoth()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];

        await store.AddCookedAsync("u1", dishId, NewKey());
        var totals = await store.AddCookedAsync("u1", dishId, NewKey());

        Assert.NotNull(totals);
        Assert.Equal(2, totals.CookedCount);
        Assert.Equal(2, (await store.GetUserFeedbackAsync("u1", dishId)).CookedCount);
    }

    [Fact]
    public async Task AddCooked_SameSearchTwice_SecondIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];
        var key = NewKey();

        await store.AddCookedAsync("u1", dishId, key);
        var second = await store.AddCookedAsync("u1", dishId, key);

        Assert.Null(second);
        Assert.True(await store.HasCookedForSearchAsync("u1", dishId, key));
        Assert.Equal(1, (await store.GetAllTotalsAsync())[dishId].CookedCount);
    }

    [Fact]
    public async Task AddCooked_SameSearchOtherDish_IsAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishIds = await database.GetDishIdsAsync(2);
        var key = NewKey();

        await store.AddCookedAsync("u1", dishIds[0], key);
        var second = await store.AddCookedAsync("u1", dishIds[1], key);

        Assert.NotNull(second);
        Assert.False(await store.HasCookedForSearchAsync("u1", dishIds[0], NewKey()));
    }

    [Fact]
    public async Task SetVote_Up_ThenDown_ReplacesTheVote()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];

        await store.SetVoteAsync("u1", dishId, Vote.Up);
        var totals = await store.SetVoteAsync("u1", dishId, Vote.Down);

        Assert.Equal(new DishTotals(0, 0, 1), totals);
        Assert.Equal(Vote.Down, (await store.GetUserFeedbackAsync("u1", dishId)).Vote);
        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.DishFeedback.CountAsync());
    }

    [Fact]
    public async Task SetVote_None_RemovesTheVote()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.SetVoteAsync("u1", dishId, Vote.Up);

        var totals = await store.SetVoteAsync("u1", dishId, Vote.None);

        Assert.Equal(DishTotals.Empty, totals);
        Assert.Equal(UserDishFeedback.None, await store.GetUserFeedbackAsync("u1", dishId));
    }

    [Fact]
    public async Task SetVote_TwoUsers_BothCount()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        await database.CreateUserAsync("u2");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];

        await store.SetVoteAsync("u1", dishId, Vote.Up);
        var totals = await store.SetVoteAsync("u2", dishId, Vote.Up);

        Assert.Equal(2, totals.UpCount);
    }

    [Fact]
    public async Task SetVote_DoesNotTouchCookedRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.AddCookedAsync("u1", dishId, NewKey());

        var totals = await store.SetVoteAsync("u1", dishId, Vote.None);

        Assert.Equal(new DishTotals(1, 0, 0), totals);
    }

    [Fact]
    public async Task GetAllTotals_ReturnsOneEntryPerRatedDish()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishIds = await database.GetDishIdsAsync(3);
        await store.AddCookedAsync("u1", dishIds[0], NewKey());
        await store.SetVoteAsync("u1", dishIds[1], Vote.Down);

        var totals = await store.GetAllTotalsAsync();

        Assert.Equal(2, totals.Count);
        Assert.Equal(new DishTotals(1, 0, 0), totals[dishIds[0]]);
        Assert.Equal(new DishTotals(0, 0, 1), totals[dishIds[1]]);
        Assert.False(totals.ContainsKey(dishIds[2]));
    }

    [Fact]
    public async Task DeletingDish_CascadesFeedbackAndStats()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.AddCookedAsync("u1", dishId, NewKey());

        await using (var context = await database.CreateContextAsync())
        {
            context.Dishes.Remove(await context.Dishes.SingleAsync(dish => dish.Id == dishId));
            await context.SaveChangesAsync();
        }

        await using var check = await database.CreateContextAsync();
        Assert.Equal(0, await check.DishFeedback.CountAsync());
        Assert.Equal(0, await check.DishStats.CountAsync());
    }

    [Fact]
    public async Task DeletingUser_KeepsFeedbackWithoutUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.CreateUserAsync("u1");
        var store = CreateStore(database);
        var dishId = (await database.GetDishIdsAsync(1))[0];
        await store.AddCookedAsync("u1", dishId, NewKey());

        await using (var context = await database.CreateContextAsync())
        {
            context.Users.Remove(await context.Users.SingleAsync(user => user.Id == "u1"));
            await context.SaveChangesAsync();
        }

        await using var check = await database.CreateContextAsync();
        var row = await check.DishFeedback.SingleAsync();
        Assert.Null(row.UserId);
        Assert.Equal(1, (await check.DishStats.SingleAsync()).CookedCount);
    }
}
