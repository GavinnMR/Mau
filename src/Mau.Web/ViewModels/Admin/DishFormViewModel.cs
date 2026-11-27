using System.ComponentModel.DataAnnotations;
using Mau.Core.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mau.Web.ViewModels.Admin;

/// <summary>The create/edit dish form.</summary>
public class DishFormViewModel
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public CookingMethod CookingMethod { get; set; } = CookingMethod.Braised;

    public int? CookTimeMinutes { get; set; }

    [Display(Name = "Main protein")]
    public int? MainProteinId { get; set; }

    public string? Steps { get; set; }

    public List<DishIngredientFormRow> Ingredients { get; set; } = new();

    [BindNever]
    public IReadOnlyList<SelectListItem> IngredientOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<SelectListItem> ProteinOptions { get; set; } = [];
}
