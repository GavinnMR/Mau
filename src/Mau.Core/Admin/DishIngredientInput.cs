namespace Mau.Core.Admin;

/// <summary>One ingredient row submitted from a dish admin form.</summary>
public sealed record DishIngredientInput(
    int? IngredientId, decimal? Quantity, string? Unit, bool IsOptional);
