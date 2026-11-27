using Mau.Core.Users;

namespace Mau.Tests.Support;

public sealed class FakeNeverListStore : INeverListStore
{
    private readonly Dictionary<string, HashSet<int>> _ids = new();

    public Task<IReadOnlySet<int>> GetIngredientIdsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<int>>(
            _ids.TryGetValue(userId, out var ids) ? new HashSet<int>(ids) : new HashSet<int>());

    public Task ReplaceAsync(
        string userId, IReadOnlyCollection<int> ingredientIds, CancellationToken ct = default)
    {
        _ids[userId] = ingredientIds.ToHashSet();
        return Task.CompletedTask;
    }
}
