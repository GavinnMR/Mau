using Mau.Core.Users;

namespace Mau.Tests.Support;

public sealed class FakeFavoriteStore : IFavoriteStore
{
    private readonly Dictionary<string, List<int>> _dishes = new();

    public Task<IReadOnlyList<int>> GetDishIdsAsync(
        string userId, CancellationToken ct = default)
    {
        var ids = _dishes.TryGetValue(userId, out var stored)
            ? new List<int>(stored)
            : new List<int>();

        return Task.FromResult<IReadOnlyList<int>>(ids);
    }

    public Task<bool> ExistsAsync(string userId, int dishId, CancellationToken ct = default) =>
        Task.FromResult(_dishes.TryGetValue(userId, out var ids) && ids.Contains(dishId));

    public Task AddAsync(string userId, int dishId, CancellationToken ct = default)
    {
        if (!_dishes.TryGetValue(userId, out var ids))
        {
            ids = new List<int>();
            _dishes[userId] = ids;
        }

        if (!ids.Contains(dishId))
        {
            ids.Insert(0, dishId);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string userId, int dishId, CancellationToken ct = default)
    {
        if (_dishes.TryGetValue(userId, out var ids))
        {
            ids.Remove(dishId);
        }

        return Task.CompletedTask;
    }
}
