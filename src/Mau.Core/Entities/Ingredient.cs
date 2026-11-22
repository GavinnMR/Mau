namespace Mau.Core.Entities;

/// <summary>An ingredient in the catalog.</summary>
public class Ingredient
{
    public int Id { get; set; }
    public string CanonicalName { get; set; } = "";
    public IngredientCategory Category { get; set; }
    public bool IsStaple { get; set; }
    public List<IngredientSynonym> Synonyms { get; set; } = new();
}
