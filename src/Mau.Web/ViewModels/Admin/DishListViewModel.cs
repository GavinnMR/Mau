namespace Mau.Web.ViewModels.Admin;

/// <summary>The admin dishes list.</summary>
public sealed record DishListViewModel(
    string? Query, IReadOnlyList<DishRowViewModel> Rows);
