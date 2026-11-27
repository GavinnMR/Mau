namespace Mau.Core.Entities;

/// <summary>An ingredient a user never wants in their dishes (allergy, diet or taste).</summary>
public class NeverIngredient
{
    public string UserId { get; set; } = "";
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
}
