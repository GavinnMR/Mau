namespace Mau.Web.ViewModels;

/// <summary>The history page model.</summary>
public sealed record HistoryViewModel(IReadOnlyList<HistoryItemViewModel> Items);
