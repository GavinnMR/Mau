using Mau.Core.Catalog;

namespace Mau.Core.Feedback;

/// <summary>Feedback rules on top of the store, the catalog and the totals cache.</summary>
public sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackStore _store;
    private readonly CatalogStore _catalog;
    private readonly DishStatsCache _stats;

    public FeedbackService(IFeedbackStore store, CatalogStore catalog, DishStatsCache stats)
    {
        _store = store;
        _catalog = catalog;
        _stats = stats;
    }

    public async Task<FeedbackResult> GiveAsync(
        string userId, int dishId, FeedbackAction action, string? searchKey,
        CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(dishId))
        {
            return new FeedbackResult(
                FeedbackOutcome.DishNotFound, UserDishFeedback.None, DishTotals.Empty);
        }

        if (action == FeedbackAction.Cooked)
        {
            if (string.IsNullOrEmpty(searchKey))
            {
                return await CurrentAsync(FeedbackOutcome.SearchRequired, userId, dishId, ct);
            }

            var totals = await _store.AddCookedAsync(userId, dishId, searchKey, ct);
            if (totals is null)
            {
                return await CurrentAsync(FeedbackOutcome.AlreadyCookedForSearch, userId, dishId, ct);
            }

            return await SavedAsync(userId, dishId, totals, ct);
        }

        var wanted = action == FeedbackAction.ThumbsUp ? Vote.Up : Vote.Down;
        var current = await _store.GetUserFeedbackAsync(userId, dishId, ct);
        var newTotals = await _store.SetVoteAsync(
            userId, dishId, current.Vote == wanted ? Vote.None : wanted, ct);
        return await SavedAsync(userId, dishId, newTotals, ct);
    }

    public Task<UserDishFeedback> GetForUserAsync(
        string userId, int dishId, CancellationToken ct = default) =>
        _store.GetUserFeedbackAsync(userId, dishId, ct);

    public Task<bool> HasCookedForSearchAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default) =>
        _store.HasCookedForSearchAsync(userId, dishId, searchKey, ct);

    private async Task<FeedbackResult> SavedAsync(
        string userId, int dishId, DishTotals totals, CancellationToken ct)
    {
        _stats.Set(dishId, totals);
        var user = await _store.GetUserFeedbackAsync(userId, dishId, ct);
        return new FeedbackResult(FeedbackOutcome.Saved, user, totals);
    }

    private async Task<FeedbackResult> CurrentAsync(
        FeedbackOutcome outcome, string userId, int dishId, CancellationToken ct)
    {
        var user = await _store.GetUserFeedbackAsync(userId, dishId, ct);
        return new FeedbackResult(outcome, user, _stats.Get(dishId));
    }
}
