using Mau.Core.Entities;

namespace Mau.Core.Admin;

/// <summary>A dish submitted from an admin form.</summary>
public sealed record DishInput(
    string? Name, string? Description, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> Steps, IReadOnlyList<DishIngredientInput> Ingredients,
    int? MainProteinId = null);
