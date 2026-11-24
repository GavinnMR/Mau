using Mau.Core.Search;

namespace Mau.Core.Users;

/// <summary>Search history rules on top of the log store and the search service.</summary>
public sealed class SearchHistoryService : ISearchHistoryService
{
    private readonly ISearchLogStore _store;
    private readonly ISearchService _searchService;

    public SearchHistoryService(ISearchLogStore store, ISearchService searchService)
    {
        _store = store;
        _searchService = searchService;
    }

    public async Task<bool> RecordAsync(
        string? userId, string? text, IReadOnlyCollection<int>? excludedIds,
        CancellationToken ct = default)
    {
        var result = _searchService.Search(new SearchRequest(text, excludedIds));

        var ingredientIds = result.Tokens
            .Where(token => token.IngredientId is not null && !token.IsExcluded)
            .Select(token => token.IngredientId!.Value)
            .Distinct()
            .ToList();

        if (ingredientIds.Count == 0)
        {
            return false;
        }

        await _store.AddAsync(userId, ingredientIds, result.Dishes.Count, ct);

        if (userId is not null)
        {
            await _store.TrimAsync(userId, ISearchHistoryService.KeepPerUser, ct);
        }

        return true;
    }

    public Task<IReadOnlyList<SearchLogEntry>> GetRecentAsync(
        string userId, CancellationToken ct = default) =>
        _store.GetRecentAsync(userId, ISearchHistoryService.PageSize, ct);

    public Task<bool> DeleteAsync(string userId, int logId, CancellationToken ct = default) =>
        _store.DeleteAsync(userId, logId, ct);

    public Task<int> ClearAsync(string userId, CancellationToken ct = default) =>
        _store.DeleteAllAsync(userId, ct);
}
