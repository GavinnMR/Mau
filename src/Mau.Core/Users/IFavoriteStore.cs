namespace Mau.Core.Users;

/// <summary>Persistence for a user's favorite dishes.</summary>
public interface IFavoriteStore
{
    Task<IReadOnlyList<int>> GetDishIdsAsync(string userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string userId, int dishId, CancellationToken ct = default);
    Task AddAsync(string userId, int dishId, CancellationToken ct = default);
    Task RemoveAsync(string userId, int dishId, CancellationToken ct = default);
}
