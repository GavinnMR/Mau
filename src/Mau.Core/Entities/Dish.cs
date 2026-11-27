namespace Mau.Core.Entities;

/// <summary>A Filipino dish in the catalog.</summary>
public class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CookingMethod CookingMethod { get; set; }
    public int CookTimeMinutes { get; set; }

    /// <summary>The dish's main protein (one of its ingredients), used by user preferences.</summary>
    public int? MainProteinId { get; set; }
    public Ingredient? MainProtein { get; set; }
    public string Steps { get; set; } = "";
    public List<DishIngredient> Ingredients { get; set; } = new();
}
