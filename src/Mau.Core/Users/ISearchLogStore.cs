namespace Mau.Core.Users;

/// <summary>Persistence for recorded searches.</summary>
public interface ISearchLogStore
{
    Task AddAsync(
        string? userId, IReadOnlyCollection<int> ingredientIds, int resultCount,
        CancellationToken ct = default);

    Task<IReadOnlyList<SearchLogEntry>> GetRecentAsync(
        string userId, int max, CancellationToken ct = default);

    Task<bool> DeleteAsync(string userId, int logId, CancellationToken ct = default);
    Task<int> DeleteAllAsync(string userId, CancellationToken ct = default);
    Task TrimAsync(string userId, int keep, CancellationToken ct = default);
}
