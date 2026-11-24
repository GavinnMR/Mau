using Mau.Core.Catalog;

namespace Mau.Core.Users;

/// <summary>Pantry rules on top of the store and the catalog.</summary>
public sealed class PantryService : IPantryService
{
    private readonly IPantryStore _store;
    private readonly CatalogStore _catalog;

    public PantryService(IPantryStore store, CatalogStore catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    public Task<IReadOnlySet<int>> GetIdsAsync(string userId, CancellationToken ct = default) =>
        _store.GetIngredientIdsAsync(userId, ct);

    public async Task<int> SaveAsync(
        string userId, IEnumerable<int>? ingredientIds, CancellationToken ct = default)
    {
        var ids = (ingredientIds ?? Array.Empty<int>())
            .Where(id => _catalog.Current.IngredientsById.TryGetValue(id, out var ingredient)
                && !ingredient.IsStaple)
            .Distinct()
            .ToList();

        await _store.ReplaceAsync(userId, ids, ct);
        return ids.Count;
    }
}
