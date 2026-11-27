namespace Mau.Core.Feedback;

/// <summary>Feedback totals for one dish.</summary>
public sealed record DishTotals(int CookedCount, int UpCount, int DownCount)
{
    public static DishTotals Empty { get; } = new(0, 0, 0);
}

/// <summary>One user's feedback on one dish.</summary>
public sealed record UserDishFeedback(int CookedCount, Vote Vote)
{
    public static UserDishFeedback None { get; } = new(0, Vote.None);
}

/// <summary>A user's current vote on a dish.</summary>
public enum Vote { None, Up, Down }
