namespace Mau.Core.Entities;

/// <summary>One included ingredient of a recorded search.</summary>
public class SearchLogIngredient
{
    public int SearchLogId { get; set; }
    public SearchLog? SearchLog { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
}
