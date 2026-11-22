namespace Mau.Web.ViewModels;

/// <summary>One ingredient row on the dish details page.</summary>
public sealed record DishIngredientViewModel(
    string Name, string AmountText, bool IsOptional, bool IsStaple);
