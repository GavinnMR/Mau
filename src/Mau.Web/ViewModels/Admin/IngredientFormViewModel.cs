using Mau.Core.Entities;

namespace Mau.Web.ViewModels.Admin;

/// <summary>The create/edit ingredient form.</summary>
public class IngredientFormViewModel
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public IngredientCategory Category { get; set; } = IngredientCategory.Other;

    public bool IsStaple { get; set; }

    public List<SynonymFormRow> Synonyms { get; set; } = new();
}
