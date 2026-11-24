using Mau.Core.Users;

namespace Mau.Tests.Support;

public sealed class FakeSearchLogStore : ISearchLogStore
{
    private readonly List<Row> _rows = new();
    private int _nextId = 1;
    private DateTime _nextTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public IReadOnlyList<Row> Rows => _rows;

    public int TrimCallCount { get; private set; }

    public string? LastTrimUserId { get; private set; }

    public int LastTrimKeep { get; private set; }

    public Task AddAsync(
        string? userId, IReadOnlyCollection<int> ingredientIds, int resultCount,
        CancellationToken ct = default)
    {
        var entry = new SearchLogEntry(
            _nextId++,
            _nextTime,
            resultCount,
            ingredientIds.Distinct().OrderBy(id => id).ToList());

        _nextTime = _nextTime.AddMinutes(1);
        _rows.Add(new Row(userId, entry));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchLogEntry>> GetRecentAsync(
        string userId, int max, CancellationToken ct = default)
    {
        var entries = _rows
            .Where(row => row.UserId == userId)
            .OrderByDescending(row => row.Entry.SearchedAtUtc)
            .ThenByDescending(row => row.Entry.Id)
            .Take(max)
            .Select(row => row.Entry)
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchLogEntry>>(entries);
    }

    public Task<bool> DeleteAsync(string userId, int logId, CancellationToken ct = default)
    {
        var row = _rows.FirstOrDefault(
            candidate => candidate.UserId == userId && candidate.Entry.Id == logId);

        if (row is null)
        {
            return Task.FromResult(false);
        }

        _rows.Remove(row);
        return Task.FromResult(true);
    }

    public Task<int> DeleteAllAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_rows.RemoveAll(row => row.UserId == userId));

    public Task TrimAsync(string userId, int keep, CancellationToken ct = default)
    {
        TrimCallCount++;
        LastTrimUserId = userId;
        LastTrimKeep = keep;

        var userRows = _rows
            .Where(row => row.UserId == userId)
            .OrderByDescending(row => row.Entry.SearchedAtUtc)
            .ThenByDescending(row => row.Entry.Id)
            .ToList();

        foreach (var row in userRows.Skip(keep))
        {
            _rows.Remove(row);
        }

        return Task.CompletedTask;
    }

    public sealed record Row(string? UserId, SearchLogEntry Entry);
}
