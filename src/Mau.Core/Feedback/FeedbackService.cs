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

    public async Task<FeedbackResult?> GiveAsync(
        string userId, int dishId, FeedbackAction action, CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(dishId))
        {
            return null;
        }

        DishTotals totals;
        if (action == FeedbackAction.Cooked)
        {
            totals = await _store.AddCookedAsync(userId, dishId, ct);
        }
        else
        {
            var wanted = action == FeedbackAction.ThumbsUp ? Vote.Up : Vote.Down;
            var current = await _store.GetUserFeedbackAsync(userId, dishId, ct);
            totals = await _store.SetVoteAsync(
                userId, dishId, current.Vote == wanted ? Vote.None : wanted, ct);
        }

        _stats.Set(dishId, totals);
        var user = await _store.GetUserFeedbackAsync(userId, dishId, ct);
        return new FeedbackResult(user, totals);
    }

    public Task<UserDishFeedback> GetForUserAsync(
        string userId, int dishId, CancellationToken ct = default) =>
        _store.GetUserFeedbackAsync(userId, dishId, ct);
}
