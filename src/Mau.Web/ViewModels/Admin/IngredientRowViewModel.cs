namespace Mau.Web.ViewModels.Admin;

/// <summary>One ingredient row on the admin list.</summary>
public sealed record IngredientRowViewModel(
    int Id, string Name, string Category, bool IsStaple, string Synonyms, int UsedByDishes);
