namespace Mau.Core.Users;

/// <summary>Reads and saves a user's pantry.</summary>
public interface IPantryService
{
    Task<IReadOnlySet<int>> GetIdsAsync(string userId, CancellationToken ct = default);
    Task<int> SaveAsync(
        string userId, IEnumerable<int>? ingredientIds, CancellationToken ct = default);
}
