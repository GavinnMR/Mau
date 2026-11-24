using Mau.Core.Catalog;

namespace Mau.Core.Users;

/// <summary>Favorite rules on top of the store and the catalog.</summary>
public sealed class FavoriteService : IFavoriteService
{
    private readonly IFavoriteStore _store;
    private readonly CatalogStore _catalog;

    public FavoriteService(IFavoriteStore store, CatalogStore catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<int>> GetDishIdsAsync(
        string userId, CancellationToken ct = default)
    {
        var ids = await _store.GetDishIdsAsync(userId, ct);
        var dishes = _catalog.Current.DishesById;
        return ids.Where(dishes.ContainsKey).ToList();
    }

    public Task<bool> IsFavoriteAsync(string userId, int dishId, CancellationToken ct = default) =>
        _store.ExistsAsync(userId, dishId, ct);

    public async Task<bool> AddAsync(string userId, int dishId, CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(dishId))
        {
            return false;
        }

        await _store.AddAsync(userId, dishId, ct);
        return true;
    }

    public async Task<bool> RemoveAsync(string userId, int dishId, CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(dishId))
        {
            return false;
        }

        await _store.RemoveAsync(userId, dishId, ct);
        return true;
    }
}
