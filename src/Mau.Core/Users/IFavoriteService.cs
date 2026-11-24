namespace Mau.Core.Users;

/// <summary>Reads and changes a user's favorite dishes.</summary>
public interface IFavoriteService
{
    Task<IReadOnlyList<int>> GetDishIdsAsync(string userId, CancellationToken ct = default);
    Task<bool> IsFavoriteAsync(string userId, int dishId, CancellationToken ct = default);
    Task<bool> AddAsync(string userId, int dishId, CancellationToken ct = default);
    Task<bool> RemoveAsync(string userId, int dishId, CancellationToken ct = default);
}
