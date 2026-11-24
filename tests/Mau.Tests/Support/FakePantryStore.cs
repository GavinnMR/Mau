using Mau.Core.Users;

namespace Mau.Tests.Support;

public sealed class FakePantryStore : IPantryStore
{
    private readonly Dictionary<string, HashSet<int>> _items = new();

    public Task<IReadOnlySet<int>> GetIngredientIdsAsync(
        string userId, CancellationToken ct = default)
    {
        var ids = _items.TryGetValue(userId, out var stored)
            ? new HashSet<int>(stored)
            : new HashSet<int>();

        return Task.FromResult<IReadOnlySet<int>>(ids);
    }

    public Task ReplaceAsync(
        string userId, IReadOnlyCollection<int> ingredientIds, CancellationToken ct = default)
    {
        _items[userId] = ingredientIds.ToHashSet();
        return Task.CompletedTask;
    }
}
