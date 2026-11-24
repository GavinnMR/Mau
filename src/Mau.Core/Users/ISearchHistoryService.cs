namespace Mau.Core.Users;

/// <summary>Records and reads a user's recent searches.</summary>
public interface ISearchHistoryService
{
    const int PageSize = 20;
    const int KeepPerUser = 100;

    Task<bool> RecordAsync(
        string? userId, string? text, IReadOnlyCollection<int>? excludedIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<SearchLogEntry>> GetRecentAsync(
        string userId, CancellationToken ct = default);

    Task<bool> DeleteAsync(string userId, int logId, CancellationToken ct = default);
    Task<int> ClearAsync(string userId, CancellationToken ct = default);
}
