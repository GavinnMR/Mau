namespace Mau.Core.Feedback;

/// <summary>Feedback totals for one dish.</summary>
public sealed record DishTotals(int CookedCount, int UpCount, int DownCount)
{
    public static DishTotals Empty { get; } = new(0, 0, 0);

    /// <summary>2 per cook, +1 per thumbs up, -1 per thumbs down, never below 0.</summary>
    public int Points => Math.Max(0, (2 * CookedCount) + UpCount - DownCount);
}

/// <summary>One user's feedback on one dish.</summary>
public sealed record UserDishFeedback(int CookedCount, Vote Vote)
{
    public static UserDishFeedback None { get; } = new(0, Vote.None);
}

/// <summary>A user's current vote on a dish.</summary>
public enum Vote { None, Up, Down }
