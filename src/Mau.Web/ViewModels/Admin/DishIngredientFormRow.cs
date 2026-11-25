namespace Mau.Web.ViewModels.Admin;

/// <summary>One editable dish ingredient row.</summary>
public class DishIngredientFormRow
{
    public int? IngredientId { get; set; }

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public bool IsOptional { get; set; }
}
