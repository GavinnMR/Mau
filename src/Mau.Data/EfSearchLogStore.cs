using Mau.Core.Entities;
using Mau.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores recorded searches in the database.</summary>
public sealed class EfSearchLogStore : ISearchLogStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public EfSearchLogStore(
        IDbContextFactory<MauDbContext> contextFactory, TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task AddAsync(
        string? userId, IReadOnlyCollection<int> ingredientIds, int resultCount,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var log = new SearchLog
        {
            UserId = userId,
            SearchedAt = _timeProvider.GetUtcNow().UtcDateTime,
            ResultCount = resultCount,
            Ingredients = ingredientIds
                .Distinct()
                .Select(ingredientId => new SearchLogIngredient { IngredientId = ingredientId })
                .ToList(),
        };

        context.SearchLogs.Add(log);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SearchLogEntry>> GetRecentAsync(
        string userId, int max, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var logs = await context.SearchLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.SearchedAt)
            .ThenByDescending(log => log.Id)
            .Take(max)
            .Include(log => log.Ingredients)
            .ToListAsync(ct);

        return logs
            .Select(log => new SearchLogEntry(
                log.Id,
                log.SearchedAt,
                log.ResultCount,
                log.Ingredients
                    .Select(ingredient => ingredient.IngredientId)
                    .OrderBy(ingredientId => ingredientId)
                    .ToList()))
            .ToList();
    }

    public async Task<bool> DeleteAsync(
        string userId, int logId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var log = await context.SearchLogs
            .FirstOrDefaultAsync(
                candidate => candidate.Id == logId && candidate.UserId == userId, ct);

        if (log is null)
        {
            return false;
        }

        context.SearchLogs.Remove(log);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> DeleteAllAsync(string userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var logs = await context.SearchLogs
            .Where(log => log.UserId == userId)
            .ToListAsync(ct);

        context.SearchLogs.RemoveRange(logs);
        await context.SaveChangesAsync(ct);
        return logs.Count;
    }

    public async Task TrimAsync(string userId, int keep, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var staleIds = await context.SearchLogs
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.SearchedAt)
            .ThenByDescending(log => log.Id)
            .Skip(keep)
            .Select(log => log.Id)
            .ToListAsync(ct);

        if (staleIds.Count == 0)
        {
            return;
        }

        var stale = await context.SearchLogs
            .Where(log => staleIds.Contains(log.Id))
            .ToListAsync(ct);

        context.SearchLogs.RemoveRange(stale);
        await context.SaveChangesAsync(ct);
    }
}
