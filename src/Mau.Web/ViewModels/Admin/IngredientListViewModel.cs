namespace Mau.Web.ViewModels.Admin;

/// <summary>The admin ingredients list.</summary>
public sealed record IngredientListViewModel(
    string? Query, IReadOnlyList<IngredientRowViewModel> Rows);
