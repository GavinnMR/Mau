namespace Mau.Core.Users;

/// <summary>Persistence for a user's pantry ingredients.</summary>
public interface IPantryStore
{
    Task<IReadOnlySet<int>> GetIngredientIdsAsync(string userId, CancellationToken ct = default);
    Task ReplaceAsync(string userId, IReadOnlyCollection<int> ingredientIds, CancellationToken ct = default);
}
