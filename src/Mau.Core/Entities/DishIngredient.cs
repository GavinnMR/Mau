namespace Mau.Core.Entities;

/// <summary>One ingredient row of a dish.</summary>
public class DishIngredient
{
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public int Position { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public bool IsOptional { get; set; }
}
