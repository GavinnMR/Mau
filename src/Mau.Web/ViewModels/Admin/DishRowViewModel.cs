namespace Mau.Web.ViewModels.Admin;

/// <summary>One dish row on the admin list.</summary>
public sealed record DishRowViewModel(
    int Id, string Name, string CookingMethod, int CookTimeMinutes, int IngredientCount);
