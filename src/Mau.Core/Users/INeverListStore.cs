namespace Mau.Core.Users;

/// <summary>Persistence for the ingredients a user never wants.</summary>
public interface INeverListStore
{
    Task<IReadOnlySet<int>> GetIngredientIdsAsync(string userId, CancellationToken ct = default);

    Task ReplaceAsync(string userId, IReadOnlyCollection<int> ingredientIds, CancellationToken ct = default);
}
