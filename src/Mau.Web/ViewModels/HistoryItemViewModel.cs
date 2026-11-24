namespace Mau.Web.ViewModels;

/// <summary>One recorded search on the history page.</summary>
public sealed record HistoryItemViewModel(
    int Id, string WhenText, int ResultCount,
    IReadOnlyList<string> IngredientNames, string SearchQuery);
