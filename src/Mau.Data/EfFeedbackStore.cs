using Mau.Core.Entities;
using Mau.Core.Feedback;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores feedback rows and keeps each dish's totals in step, in one transaction.</summary>
public sealed class EfFeedbackStore : IFeedbackStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public EfFeedbackStore(
        IDbContextFactory<MauDbContext> contextFactory, TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<DishTotals?> AddCookedAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default)
    {
        if (await HasCookedForSearchAsync(userId, dishId, searchKey, ct))
        {
            return null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var row = NewRow(userId, dishId, FeedbackType.Cooked);
        row.SearchKey = searchKey;
        context.DishFeedback.Add(row);
        var stat = await GetOrAddStatAsync(context, dishId, ct);
        stat.CookedCount++;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two clicks raced; the unique index kept the first one.
            if (await HasCookedForSearchAsync(userId, dishId, searchKey, ct))
            {
                return null;
            }

            throw;
        }

        await transaction.CommitAsync(ct);
        return ToTotals(stat);
    }

    public async Task<bool> HasCookedForSearchAsync(
        string userId, int dishId, string searchKey, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.DishFeedback.AnyAsync(
            row => row.UserId == userId && row.DishId == dishId && row.SearchKey == searchKey, ct);
    }

    public async Task<DishTotals> SetVoteAsync(
        string userId, int dishId, Vote vote, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var stat = await GetOrAddStatAsync(context, dishId, ct);

        var oldVotes = await context.DishFeedback
            .Where(row => row.UserId == userId && row.DishId == dishId &&
                (row.Type == FeedbackType.ThumbsUp || row.Type == FeedbackType.ThumbsDown))
            .ToListAsync(ct);

        foreach (var old in oldVotes)
        {
            if (old.Type == FeedbackType.ThumbsUp)
            {
                stat.UpCount = Math.Max(0, stat.UpCount - 1);
            }
            else
            {
                stat.DownCount = Math.Max(0, stat.DownCount - 1);
            }
        }

        context.DishFeedback.RemoveRange(oldVotes);

        if (vote == Vote.Up)
        {
            context.DishFeedback.Add(NewRow(userId, dishId, FeedbackType.ThumbsUp));
            stat.UpCount++;
        }
        else if (vote == Vote.Down)
        {
            context.DishFeedback.Add(NewRow(userId, dishId, FeedbackType.ThumbsDown));
            stat.DownCount++;
        }

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTotals(stat);
    }

    public async Task<UserDishFeedback> GetUserFeedbackAsync(
        string userId, int dishId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var types = await context.DishFeedback
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.DishId == dishId)
            .Select(row => row.Type)
            .ToListAsync(ct);

        var vote = types.Contains(FeedbackType.ThumbsUp) ? Vote.Up
            : types.Contains(FeedbackType.ThumbsDown) ? Vote.Down
            : Vote.None;

        return new UserDishFeedback(types.Count(type => type == FeedbackType.Cooked), vote);
    }

    public async Task<IReadOnlyDictionary<int, DishTotals>> GetAllTotalsAsync(
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.DishStats
            .AsNoTracking()
            .ToDictionaryAsync(stat => stat.DishId, ToTotals, ct);
    }

    private DishFeedback NewRow(string userId, int dishId, FeedbackType type) => new()
    {
        UserId = userId,
        DishId = dishId,
        Type = type,
        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
    };

    private static async Task<DishStat> GetOrAddStatAsync(
        MauDbContext context, int dishId, CancellationToken ct)
    {
        var stat = await context.DishStats.FirstOrDefaultAsync(row => row.DishId == dishId, ct);
        if (stat is null)
        {
            stat = new DishStat { DishId = dishId };
            context.DishStats.Add(stat);
        }

        return stat;
    }

    private static DishTotals ToTotals(DishStat stat) =>
        new(stat.CookedCount, stat.UpCount, stat.DownCount);
}
