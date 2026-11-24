namespace Mau.Core.Admin;

/// <summary>Validates and applies catalog changes from the admin pages.</summary>
public interface ICatalogAdminService
{
    Task<AdminResult> CreateIngredientAsync(IngredientInput input, CancellationToken ct = default);
    Task<AdminResult> UpdateIngredientAsync(int id, IngredientInput input, CancellationToken ct = default);
    Task<AdminResult> DeleteIngredientAsync(int id, CancellationToken ct = default);
    Task<AdminResult> CreateDishAsync(DishInput input, CancellationToken ct = default);
    Task<AdminResult> UpdateDishAsync(int id, DishInput input, CancellationToken ct = default);
    Task<AdminResult> DeleteDishAsync(int id, CancellationToken ct = default);
}
