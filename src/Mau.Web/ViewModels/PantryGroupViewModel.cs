namespace Mau.Web.ViewModels;

/// <summary>One category of pantry checkboxes.</summary>
public sealed record PantryGroupViewModel(
    string Title, IReadOnlyList<PantryOptionViewModel> Options);
