namespace Mau.Web.ViewModels;

/// <summary>The dish details page model.</summary>
public sealed record DishDetailsViewModel(
    int Id, string Name, string Description, string CookingMethod, int CookTimeMinutes,
    IReadOnlyList<DishIngredientViewModel> Ingredients, IReadOnlyList<string> Steps,
    bool IsAuthenticated, bool IsFavorite, DishFeedbackViewModel? Feedback = null);

/// <summary>Feedback totals for a dish, plus the viewer's own feedback when logged in.</summary>
/// <remarks>
/// CookedState: "available" (opened from a search, not yet marked), "done" (already marked for
/// this search) or "needs-search" (not opened from a search). SearchToken is the link's token.
/// </remarks>
public sealed record DishFeedbackViewModel(
    int CookedCount, int UpCount, int DownCount, int CookedByYou, string Vote,
    string CookedState = "needs-search", string? SearchToken = null);
