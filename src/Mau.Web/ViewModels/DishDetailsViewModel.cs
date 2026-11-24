namespace Mau.Web.ViewModels;

/// <summary>The dish details page model.</summary>
public sealed record DishDetailsViewModel(
    int Id, string Name, string Description, string CookingMethod, int CookTimeMinutes,
    IReadOnlyList<DishIngredientViewModel> Ingredients, IReadOnlyList<string> Steps,
    bool IsAuthenticated, bool IsFavorite);
