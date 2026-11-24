using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class UserServicesTests
{
    [Fact]
    public async Task Pantry_Save_KeepsOnlyKnownNonStapleDistinctIds()
    {
        var store = new FakePantryStore();
        var service = new PantryService(store, await CreateCatalogAsync());

        var saved = await service.SaveAsync("u1", new[] { 1, 2, 8, 999, 1 });

        Assert.Equal(2, saved);
        Assert.Equal(new[] { 1, 2 }, (await store.GetIngredientIdsAsync("u1")).OrderBy(id => id));
    }

    [Fact]
    public async Task Pantry_Save_Null_ReturnsZeroAndClearsStore()
    {
        var store = new FakePantryStore();
        var service = new PantryService(store, await CreateCatalogAsync());
        await service.SaveAsync("u1", new[] { 1, 2 });

        var saved = await service.SaveAsync("u1", null);

        Assert.Equal(0, saved);
        Assert.Empty(await store.GetIngredientIdsAsync("u1"));
    }

    [Fact]
    public async Task Pantry_GetIds_ReturnsStoreContents()
    {
        var store = new FakePantryStore();
        var service = new PantryService(store, await CreateCatalogAsync());
        await service.SaveAsync("u1", new[] { 2, 5 });

        var ids = await service.GetIdsAsync("u1");

        Assert.Equal(new[] { 2, 5 }, ids.OrderBy(id => id));
    }

    [Fact]
    public async Task Pantry_TwoUsers_DoNotSeeEachOther()
    {
        var store = new FakePantryStore();
        var service = new PantryService(store, await CreateCatalogAsync());
        await service.SaveAsync("u1", new[] { 1 });
        await service.SaveAsync("u2", new[] { 2 });

        Assert.Equal(new[] { 1 }, (await service.GetIdsAsync("u1")).OrderBy(id => id));
        Assert.Equal(new[] { 2 }, (await service.GetIdsAsync("u2")).OrderBy(id => id));
    }

    [Fact]
    public async Task Favorites_AddExistingDish_ReturnsTrueAndStoresIt()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());

        Assert.True(await service.AddAsync("u1", 1));
        Assert.True(await store.ExistsAsync("u1", 1));
    }

    [Fact]
    public async Task Favorites_AddUnknownDish_ReturnsFalseAndLeavesStoreUntouched()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());

        Assert.False(await service.AddAsync("u1", 999));
        Assert.Empty(await store.GetDishIdsAsync("u1"));
    }

    [Fact]
    public async Task Favorites_RemoveExisting_ReturnsTrueAndRemoves()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());
        await service.AddAsync("u1", 1);

        Assert.True(await service.RemoveAsync("u1", 1));
        Assert.False(await store.ExistsAsync("u1", 1));
    }

    [Fact]
    public async Task Favorites_RemoveUnknownDish_ReturnsFalse()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());

        Assert.False(await service.RemoveAsync("u1", 999));
    }

    [Fact]
    public async Task Favorites_AddTwice_ReturnsTrueTwiceAndKeepsOneEntry()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());

        Assert.True(await service.AddAsync("u1", 1));
        Assert.True(await service.AddAsync("u1", 1));
        Assert.Equal(new[] { 1 }, await store.GetDishIdsAsync("u1"));
    }

    [Fact]
    public async Task Favorites_GetDishIds_DropsDishesMissingFromCatalog()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());
        await store.AddAsync("u1", 1);
        await store.AddAsync("u1", 999);

        Assert.Equal(new[] { 1 }, await service.GetDishIdsAsync("u1"));
    }

    [Fact]
    public async Task Favorites_IsFavorite_PassesThroughToStore()
    {
        var store = new FakeFavoriteStore();
        var service = new FavoriteService(store, await CreateCatalogAsync());
        await store.AddAsync("u1", 1);

        Assert.True(await service.IsFavoriteAsync("u1", 1));
        Assert.False(await service.IsFavoriteAsync("u1", 2));
    }

    [Fact]
    public async Task History_RecordGuest_ChickenAndGarlic_LogsIdsAndResultCount()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        Assert.True(await history.RecordAsync(null, "chicken, garlic,", null));

        var row = Assert.Single(store.Rows);
        Assert.Null(row.UserId);
        Assert.Equal(new[] { 1, 5 }, row.Entry.IngredientIds);
        Assert.Equal(3, row.Entry.ResultCount);
    }

    [Fact]
    public async Task History_RecordLoggedIn_ExcludedPork_LowersResultCount()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        Assert.True(await history.RecordAsync("u1", "toyo,", new[] { 2 }));

        var row = Assert.Single(store.Rows);
        Assert.Equal("u1", row.UserId);
        Assert.Equal(new[] { 3 }, row.Entry.IngredientIds);
        Assert.Equal(1, row.Entry.ResultCount);
    }

    [Fact]
    public async Task History_RecordChickenAndPorkWithPorkExcluded_LogsChickenOnly()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        Assert.True(await history.RecordAsync("u1", "chicken, pork,", new[] { 2 }));

        var row = Assert.Single(store.Rows);
        Assert.Equal(new[] { 1 }, row.Entry.IngredientIds);
    }

    [Theory]
    [InlineData("dragonfruit,", null)]
    [InlineData("", null)]
    [InlineData("pork,", new[] { 2 })]
    public async Task History_RecordNothingToLog_ReturnsFalseAndLogsNothing(
        string text, int[]? excludedIds)
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        Assert.False(await history.RecordAsync("u1", text, excludedIds));
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task History_RecordSynonymAndCanonical_LogsOneId()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        Assert.True(await history.RecordAsync("u1", "toyo, soy sauce,", null));

        var row = Assert.Single(store.Rows);
        Assert.Equal(new[] { 3 }, row.Entry.IngredientIds);
    }

    [Fact]
    public async Task History_RecordLoggedIn_TrimsOnceWithKeepPerUser()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        await history.RecordAsync("u1", "chicken,", null);

        Assert.Equal(1, store.TrimCallCount);
        Assert.Equal("u1", store.LastTrimUserId);
        Assert.Equal(100, store.LastTrimKeep);
    }

    [Fact]
    public async Task History_RecordGuest_NeverTrims()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());

        await history.RecordAsync(null, "chicken,", null);

        Assert.Equal(0, store.TrimCallCount);
    }

    [Fact]
    public async Task History_Record_UserWithHundredLogs_KeepsNewestHundred()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());
        for (var i = 0; i < 100; i++)
        {
            await store.AddAsync("u1", new[] { 1 }, 1);
        }

        Assert.True(await history.RecordAsync("u1", "chicken,", null));

        var entries = await store.GetRecentAsync("u1", 200);
        Assert.Equal(100, entries.Count);
        Assert.Equal(101, entries[0].Id);
        Assert.Equal(2, entries[^1].Id);
    }

    [Fact]
    public async Task History_GetRecent_ReturnsAtMostTwentyNewestFirst()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());
        for (var i = 0; i < 25; i++)
        {
            await store.AddAsync("u1", new[] { 1 }, 1);
        }

        var entries = await history.GetRecentAsync("u1");

        Assert.Equal(20, entries.Count);
        Assert.Equal(25, entries[0].Id);
        Assert.Equal(6, entries[^1].Id);
    }

    [Fact]
    public async Task History_Delete_PassesThroughStoreResult()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());
        await store.AddAsync("u1", new[] { 1 }, 1);
        var id = store.Rows[0].Entry.Id;

        Assert.True(await history.DeleteAsync("u1", id));
        Assert.False(await history.DeleteAsync("u1", id));
        Assert.Empty(store.Rows);
    }

    [Fact]
    public async Task History_Clear_ReturnsRowsDeletedForThatUser()
    {
        var store = new FakeSearchLogStore();
        var history = CreateHistory(store, await CreateCatalogAsync());
        await store.AddAsync("u1", new[] { 1 }, 1);
        await store.AddAsync("u1", new[] { 2 }, 1);
        await store.AddAsync("u2", new[] { 3 }, 1);

        Assert.Equal(2, await history.ClearAsync("u1"));
        Assert.Single(store.Rows);
    }

    private static async Task<CatalogStore> CreateCatalogAsync()
    {
        var store = new CatalogStore(new FakeCatalogLoader(TestCatalog.CreateSnapshot()));
        await store.ReloadAsync();
        return store;
    }

    private static SearchHistoryService CreateHistory(
        FakeSearchLogStore store, CatalogStore catalog) =>
        new(store, new SearchService(catalog));
}
