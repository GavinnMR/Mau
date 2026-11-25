namespace Mau.Web.ViewModels.Admin;

/// <summary>The ingredient delete confirmation.</summary>
public sealed record IngredientDeleteViewModel(
    int Id, string Name, IReadOnlyList<string> UsedByDishNames);
