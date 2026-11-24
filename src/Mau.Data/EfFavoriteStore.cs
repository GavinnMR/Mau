using Mau.Core.Entities;
using Mau.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores a user's favorite dishes in the database.</summary>
public sealed class EfFavoriteStore : IFavoriteStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public EfFavoriteStore(
        IDbContextFactory<MauDbContext> contextFactory, TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<int>> GetDishIdsAsync(
        string userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.AddedAt)
            .ThenByDescending(favorite => favorite.DishId)
            .Select(favorite => favorite.DishId)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        string userId, int dishId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Favorites
            .AnyAsync(favorite => favorite.UserId == userId && favorite.DishId == dishId, ct);
    }

    public async Task AddAsync(string userId, int dishId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        if (await context.Favorites.AnyAsync(
            favorite => favorite.UserId == userId && favorite.DishId == dishId, ct))
        {
            return;
        }

        context.Favorites.Add(new Favorite
        {
            UserId = userId,
            DishId = dishId,
            AddedAt = _timeProvider.GetUtcNow().UtcDateTime,
        });

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (await ExistsAsync(userId, dishId, ct))
            {
                return;
            }

            throw;
        }
    }

    public async Task RemoveAsync(string userId, int dishId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var favorite = await context.Favorites
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.DishId == dishId, ct);

        if (favorite is null)
        {
            return;
        }

        context.Favorites.Remove(favorite);
        await context.SaveChangesAsync(ct);
    }
}
