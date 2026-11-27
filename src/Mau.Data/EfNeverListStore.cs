using Mau.Core.Entities;
using Mau.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores the ingredients a user never wants.</summary>
public sealed class EfNeverListStore : INeverListStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;

    public EfNeverListStore(IDbContextFactory<MauDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlySet<int>> GetIngredientIdsAsync(
        string userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var ids = await context.NeverIngredients
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.IngredientId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task ReplaceAsync(
        string userId, IReadOnlyCollection<int> ingredientIds, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.NeverIngredients
            .Where(item => item.UserId == userId)
            .ToListAsync(ct);

        context.NeverIngredients.RemoveRange(existing);
        context.NeverIngredients.AddRange(ingredientIds.Distinct().Select(id => new NeverIngredient
        {
            UserId = userId,
            IngredientId = id,
        }));

        await context.SaveChangesAsync(ct);
    }
}
