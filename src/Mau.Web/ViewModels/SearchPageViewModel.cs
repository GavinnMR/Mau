namespace Mau.Web.ViewModels;

/// <summary>The search page model.</summary>
/// <remarks>
/// SavedMaxCookTime is the user's saved time limit (the "Max time" control starts there).
/// PreferenceSummary names the saved protein and method, e.g. "chicken, Soup"; null when none.
/// </remarks>
public sealed record SearchPageViewModel(
    bool IsAuthenticated, int PantryCount, int? SavedMaxCookTime = null,
    string? PreferenceSummary = null)
{
    /// <summary>The choices in the "Max time" control, including the saved limit.</summary>
    public IReadOnlyList<int> TimeChoices =>
        new[] { 15, 20, 30, 45, 60, 90, 120 }
            .Concat(SavedMaxCookTime is int saved ? new[] { saved } : [])
            .Distinct()
            .Order()
            .ToList();
}
