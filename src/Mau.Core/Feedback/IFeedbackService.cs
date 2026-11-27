namespace Mau.Core.Feedback;

/// <summary>What a user did on a dish page.</summary>
public enum FeedbackAction { Cooked, ThumbsUp, ThumbsDown }

/// <summary>The user's feedback on a dish after a change, plus the dish's totals.</summary>
public sealed record FeedbackResult(UserDishFeedback User, DishTotals Totals);

/// <summary>Records feedback and keeps the in-memory totals current (FR-09).</summary>
public interface IFeedbackService
{
    /// <summary>Applies the action, or returns null when the dish does not exist.</summary>
    /// <remarks>A thumbs action toggles: giving the vote the user already gave removes it.</remarks>
    Task<FeedbackResult?> GiveAsync(
        string userId, int dishId, FeedbackAction action, CancellationToken ct = default);

    Task<UserDishFeedback> GetForUserAsync(string userId, int dishId, CancellationToken ct = default);
}
