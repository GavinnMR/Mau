namespace Mau.Core.Feedback;

/// <summary>Where the ranking reads each dish's feedback totals from.</summary>
public interface IDishTotalsSource
{
    DishTotals Get(int dishId);
}

/// <summary>A source with no feedback at all: every dish has empty totals.</summary>
public sealed class NoDishTotals : IDishTotalsSource
{
    public static NoDishTotals Instance { get; } = new();

    public DishTotals Get(int dishId) => DishTotals.Empty;
}
