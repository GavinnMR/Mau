namespace Mau.Web.ViewModels;

/// <summary>The pantry page model.</summary>
public sealed record PantryViewModel(
    IReadOnlyList<PantryGroupViewModel> Groups, int SelectedCount);
