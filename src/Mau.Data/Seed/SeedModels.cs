using Mau.Core.Entities;

namespace Mau.Data.Seed;

internal sealed class CatalogSeed
{
    public List<SeedIngredient> Ingredients { get; set; } = new();
    public List<SeedDish> Dishes { get; set; } = new();
}

internal sealed class SeedIngredient
{
    public string Name { get; set; } = "";
    public IngredientCategory Category { get; set; }
    public bool IsStaple { get; set; }
    public List<SeedSynonym> Synonyms { get; set; } = new();
}

internal sealed class SeedSynonym
{
    public string Name { get; set; } = "";
    public SynonymLanguage Language { get; set; }
}

internal sealed class SeedDish
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CookingMethod CookingMethod { get; set; }
    public int CookTimeMinutes { get; set; }
    public List<string> Steps { get; set; } = new();
    public List<SeedDishIngredient> Ingredients { get; set; } = new();
}

internal sealed class SeedDishIngredient
{
    public string Name { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public bool IsOptional { get; set; }
}
