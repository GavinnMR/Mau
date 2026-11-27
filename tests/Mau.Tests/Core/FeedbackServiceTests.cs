using Mau.Core.Catalog;
using Mau.Core.Feedback;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class FeedbackServiceTests
{
    [Fact]
    public async Task Cooked_WithSearch_CountsAndUpdatesCache()
    {
        var (service, cache) = await CreateAsync();

        var result = await service.GiveAsync("u1", 1, FeedbackAction.Cooked, "search-1");

        Assert.Equal(FeedbackOutcome.Saved, result.Outcome);
        Assert.Equal(1, result.User.CookedCount);
        Assert.Equal(new DishTotals(1, 0, 0), cache.Get(1));
    }

    [Fact]
    public async Task Cooked_SameSearchTwice_IsRejectedAndNotCounted()
    {
        var (service, cache) = await CreateAsync();
        await service.GiveAsync("u1", 1, FeedbackAction.Cooked, "search-1");

        var result = await service.GiveAsync("u1", 1, FeedbackAction.Cooked, "search-1");

        Assert.Equal(FeedbackOutcome.AlreadyCookedForSearch, result.Outcome);
        Assert.Equal(1, result.User.CookedCount);
        Assert.Equal(new DishTotals(1, 0, 0), cache.Get(1));
    }

    [Fact]
    public async Task Cooked_NewSearch_CountsAgain()
    {
        var (service, cache) = await CreateAsync();
        await service.GiveAsync("u1", 1, FeedbackAction.Cooked, "search-1");

        var result = await service.GiveAsync("u1", 1, FeedbackAction.Cooked, "search-2");

        Assert.Equal(FeedbackOutcome.Saved, result.Outcome);
        Assert.Equal(2, cache.Get(1).CookedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Cooked_WithoutSearch_IsRejected(string? searchKey)
    {
        var (service, cache) = await CreateAsync();

        var result = await service.GiveAsync("u1", 1, FeedbackAction.Cooked, searchKey);

        Assert.Equal(FeedbackOutcome.SearchRequired, result.Outcome);
        Assert.Empty(cache.Current);
    }

    [Fact]
    public async Task ThumbsUp_NeedsNoSearch_AndTwiceTogglesOff()
    {
        var (service, cache) = await CreateAsync();

        var first = await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp, null);
        var second = await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp, null);

        Assert.Equal(Vote.Up, first.User.Vote);
        Assert.Equal(Vote.None, second.User.Vote);
        Assert.Equal(DishTotals.Empty, cache.Get(1));
    }

    [Fact]
    public async Task ThumbsDown_AfterUp_SwitchesVote()
    {
        var (service, cache) = await CreateAsync();

        await service.GiveAsync("u1", 1, FeedbackAction.ThumbsUp, null);
        var result = await service.GiveAsync("u1", 1, FeedbackAction.ThumbsDown, null);

        Assert.Equal(Vote.Down, result.User.Vote);
        Assert.Equal(new DishTotals(0, 0, 1), cache.Get(1));
    }

    [Fact]
    public async Task UnknownDish_ReturnsDishNotFoundAndStoresNothing()
    {
        var (service, cache) = await CreateAsync();

        var result = await service.GiveAsync("u1", 999, FeedbackAction.Cooked, "search-1");

        Assert.Equal(FeedbackOutcome.DishNotFound, result.Outcome);
        Assert.Empty(cache.Current);
    }

    [Fact]
    public async Task Cache_Reload_ReadsStoreTotals()
    {
        var store = new FakeFeedbackStore();
        await store.AddCookedAsync("u1", 2, "search-1");
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
