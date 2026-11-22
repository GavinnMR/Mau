using Mau.Core.Catalog;

namespace Mau.Core.Search;

/// <summary>Search, hints and dish lookup over the current catalog snapshot.</summary>
public interface ISearchService
{
    SearchResult Search(SearchRequest request);
    IReadOnlyList<IngredientHint> GetHints(string? prefix);
    CatalogDish? GetDish(int id);
}
