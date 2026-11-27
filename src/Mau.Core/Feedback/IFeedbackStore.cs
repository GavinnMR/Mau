namespace Mau.Core.Feedback;

/// <summary>Persistence for feedback rows and the per-dish totals.</summary>
/// <remarks>Every write updates the feedback rows and the dish's totals in one transaction.</remarks>
public interface IFeedbackStore
{
    /// <summary>
    /// Records one "I cooked this" event for the search the user came from and returns the dish's
    /// new totals, or null when the user already marked this dish cooked for that search.
    /// </summary>
    Task<DishTotals?> AddCookedAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default);

    Task<bool> HasCookedForSearchAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default);

    /// <summary>Replaces the user's vote on the dish (None removes it) and returns the new totals.</summary>
    Task<DishTotals> SetVoteAsync(string userId, int dishId, Vote vote, CancellationToken ct = default);

    Task<UserDishFeedback> GetUserFeedbackAsync(string userId, int dishId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, DishTotals>> GetAllTotalsAsync(CancellationToken ct = default);
}
