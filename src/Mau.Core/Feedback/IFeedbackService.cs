namespace Mau.Core.Feedback;

/// <summary>What a user did on a dish page.</summary>
public enum FeedbackAction { Cooked, ThumbsUp, ThumbsDown }

/// <summary>Whether a feedback action was saved, and why not.</summary>
public enum FeedbackOutcome
{
    Saved,
    DishNotFound,

    /// <summary>"I cooked this" needs the dish to be opened from a search result.</summary>
    SearchRequired,

    /// <summary>The user already marked this dish cooked for this search.</summary>
    AlreadyCookedForSearch,
}

/// <summary>The outcome, the user's feedback on the dish and the dish's totals afterwards.</summary>
public sealed record FeedbackResult(FeedbackOutcome Outcome, UserDishFeedback User, DishTotals Totals);

/// <summary>Records feedback and keeps the in-memory totals current (FR-09).</summary>
public interface IFeedbackService
{
    /// <summary>Applies the action for the user.</summary>
    /// <remarks>
    /// "Cooked" counts once per search: <paramref name="searchKey"/> names the search the dish was
    /// opened from. A thumbs action toggles: giving the vote the user already gave removes it.
    /// </remarks>
    Task<FeedbackResult> GiveAsync(
        string userId, int dishId, FeedbackAction action, string? searchKey,
        CancellationToken ct = default);

    Task<UserDishFeedback> GetForUserAsync(string userId, int dishId, CancellationToken ct = default);

    Task<bool> HasCookedForSearchAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default);
}
