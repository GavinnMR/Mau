namespace Mau.Web.ViewModels;

/// <summary>The favorites page model.</summary>
public sealed record FavoritesViewModel(IReadOnlyList<FavoriteItemViewModel> Items);
