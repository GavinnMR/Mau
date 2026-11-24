namespace Mau.Web.ViewModels;

/// <summary>One favorite dish on the favorites page.</summary>
public sealed record FavoriteItemViewModel(
    int DishId, string Name, string CookingMethod, int CookTimeMinutes);
