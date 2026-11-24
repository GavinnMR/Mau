using Mau.Core.Entities;
using Mau.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores a user's pantry in the database.</summary>
public sealed class EfPantryStore : IPantryStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;

    public EfPantryStore(IDbContextFactory<MauDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlySet<int>> GetIngredientIdsAsync(
        string userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var ids = await context.PantryItems
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

        var existing = await context.PantryItems
            .Where(item => item.UserId == userId)
            .ToListAsync(ct);

        context.PantryItems.RemoveRange(existing);

        foreach (var ingredientId in ingredientIds.Distinct())
        {
            context.PantryItems.Add(new PantryItem
            {
                UserId = userId,
                IngredientId = ingredientId,
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
