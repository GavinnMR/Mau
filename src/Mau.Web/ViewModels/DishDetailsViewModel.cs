namespace Mau.Web.ViewModels;

/// <summary>The dish details page model.</summary>
public sealed record DishDetailsViewModel(
    int Id, string Name, string Description, string CookingMethod, int CookTimeMinutes,
    IReadOnlyList<DishIngredientViewModel> Ingredients, IReadOnlyList<string> Steps,
    bool IsAuthenticated, bool IsFavorite, DishFeedbackViewModel? Feedback = null);

/// <summary>Feedback totals for a dish, plus the viewer's own feedback when logged in.</summary>
public sealed record DishFeedbackViewModel(
    int CookedCount, int UpCount, int DownCount, int CookedByYou, string Vote);
