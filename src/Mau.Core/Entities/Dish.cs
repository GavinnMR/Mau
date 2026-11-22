namespace Mau.Core.Entities;

/// <summary>A Filipino dish in the catalog.</summary>
public class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CookingMethod CookingMethod { get; set; }
    public int CookTimeMinutes { get; set; }
    public string Steps { get; set; } = "";
    public List<DishIngredient> Ingredients { get; set; } = new();
}
