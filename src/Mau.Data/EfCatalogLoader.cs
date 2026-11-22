using Mau.Core.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Loads the catalog from the database.</summary>
public sealed class EfCatalogLoader : ICatalogLoader
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;

    public EfCatalogLoader(IDbContextFactory<MauDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CatalogSnapshot> LoadAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var ingredients = await context.Ingredients
            .Include(ingredient => ingredient.Synonyms)
            .AsNoTracking()
            .ToListAsync(ct);

        var dishes = await context.Dishes
            .Include(dish => dish.Ingredients)
            .ThenInclude(row => row.Ingredient)
            .AsNoTracking()
            .ToListAsync(ct);

        return CatalogSnapshot.Create(ingredients, dishes);
    }
}
