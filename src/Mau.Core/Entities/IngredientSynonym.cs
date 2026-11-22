namespace Mau.Core.Entities;

/// <summary>An alternative name for an ingredient.</summary>
public class IngredientSynonym
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public string Name { get; set; } = "";
    public SynonymLanguage Language { get; set; }
}
