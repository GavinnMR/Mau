using Mau.Core.Catalog;
using Mau.Core.Feedback;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class FeedbackServiceTests
{
    [Fact]
    public async Task Cooked_CountsAndUpdatesCache()
    {
        var (service, cache) = await CreateAsync();

        var result = await service.GiveAsync("u1", 1, FeedbackAction.Cooked);

        Assert.NotNull(result);
        Assert.Equal(1, result.User.CookedCount);
        Assert.Equal(new DishTotals(1, 0, 0), cache.Get(1));
    }

    [Fact]
    public async Task ThumbsUp_Twice_TogglesOff()
    {
        var (service, cache) = await CreateAsync();

        await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp);
        var result = await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp);

        Assert.Equal(Vote.None, result!.User.Vote);
        Assert.Equal(DishTotals.Empty, cache.Get(1));
    }

    [Fact]
    public async Task ThumbsDown_AfterUp_SwitchesVote()
    {
        var (service, cache) = await CreateAsync();

        await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp);
        var result = await service.GiveAsync("u1", 1, FeedbackAction.ThumbsDown);

        Assert.Equal(Vote.Down, result!.User.Vote);
        Assert.Equal(new DishTotals(0, 0, 1), cache.Get(1));
    }

    [Fact]
    public async Task UnknownDish_ReturnsNullAndStoresNothing()
    {
        var (service, cache) = await CreateAsync();

        var result = await service.GiveAsync("u1", 999, FeedbackAction.Cooked);

        Assert.Null(result);
        Assert.Empty(cache.Current);
    }

    [Fact]
    public async Task Cache_Reload_ReadsStoreTotals()
    {
        var store = new FakeFeedbackStore();
        await store.AddCookedAsync("u1", 2);
        var cache = new DishStatsCache(store);

        await cache.ReloadAsync();

        Assert.Equal(new DishTotals(1, 0, 0), cache.Get(2));
        Assert.Equal(DishTotals.Empty, cache.Get(3));
    }

    private static async Task<(FeedbackService Service, DishStatsCache Cache)> CreateAsync()
    {
        var catalog = new CatalogStore(new FakeCatalogLoader(TestCatalog.CreateSnapshot()));
        await catalog.ReloadAsync();
        var store = new FakeFeedbackStore();
        var cache = new DishStatsCache(store);
        return (new FeedbackService(store, catalog, cache), cache);
    }
}
