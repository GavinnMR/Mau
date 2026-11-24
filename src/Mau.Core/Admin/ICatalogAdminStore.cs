namespace Mau.Core.Admin;

/// <summary>Persistence for admin catalog writes.</summary>
public interface ICatalogAdminStore
{
    Task<int> AddIngredientAsync(IngredientInput input, CancellationToken ct = default);
    Task UpdateIngredientAsync(int id, IngredientInput input, CancellationToken ct = default);
    Task DeleteIngredientAsync(int id, CancellationToken ct = default);
    Task<int> AddDishAsync(DishInput input, CancellationToken ct = default);
    Task UpdateDishAsync(int id, DishInput input, CancellationToken ct = default);
    Task DeleteDishAsync(int id, CancellationToken ct = default);
}
