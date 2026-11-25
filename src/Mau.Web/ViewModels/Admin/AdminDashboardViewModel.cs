namespace Mau.Web.ViewModels.Admin;

/// <summary>The admin dashboard counts.</summary>
public sealed record AdminDashboardViewModel(
    int DishCount, int IngredientCount, int SynonymCount);
