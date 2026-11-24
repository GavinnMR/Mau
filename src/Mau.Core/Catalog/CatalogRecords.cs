using Mau.Core.Entities;

namespace Mau.Core.Catalog;

/// <summary>An ingredient as exposed by the read model.</summary>
public sealed record CatalogIngredient(
    int Id, string CanonicalName, IngredientCategory Category, bool IsStaple,
    IReadOnlyList<string> Synonyms)
{
    public IReadOnlyList<CatalogSynonym> SynonymItems { get; init; } = [];
}

/// <summary>One ingredient row of a dish in the read model.</summary>
public sealed record CatalogDishIngredient(
    int IngredientId, string Name, decimal? Quantity, string? Unit, bool IsOptional, bool IsStaple);

/// <summary>A dish as exposed by the read model.</summary>
public sealed record CatalogDish(
    int Id, string Name, string Description, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> Steps,
    IReadOnlyList<CatalogDishIngredient> Ingredients);
