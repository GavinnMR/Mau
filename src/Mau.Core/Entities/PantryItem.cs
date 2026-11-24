namespace Mau.Core.Entities;

/// <summary>An ingredient a user usually keeps at home.</summary>
public class PantryItem
{
    public string UserId { get; set; } = "";
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
}
