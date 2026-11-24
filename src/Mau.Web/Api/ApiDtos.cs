using System.ComponentModel.DataAnnotations;
using Mau.Core.Entities;

namespace Mau.Web.Api;

public sealed record HintDto(string Text, string Ingredient);

public sealed record HintsResponseDto(IReadOnlyList<HintDto> Hints);

public sealed record TokenDto(
    int Index, string Raw, int? IngredientId, string? Name, bool IsExcluded,
    IReadOnlyList<string> Suggestions);

public sealed record DishCardDto(
    int Id, string Name, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> StillNeeded, int MissingCount);

public sealed record SearchResponseDto(
    IReadOnlyList<TokenDto> Tokens, IReadOnlyList<DishCardDto> Dishes, bool PantryApplied)
{
    public string Status => "ok";
}

public sealed record PantryIngredientDto(int Id, string Name);

public sealed record PantryResponseDto(IReadOnlyList<PantryIngredientDto> Ingredients);

public sealed record DishIngredientDto(
    int IngredientId, string Name, decimal? Quantity, string? Unit, bool IsOptional, bool IsStaple);

public sealed record DishDetailsDto(
    int Id, string Name, string Description, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<DishIngredientDto> Ingredients, IReadOnlyList<string> Steps);

public sealed class SearchRequestDto
{
    [StringLength(500, ErrorMessage = "Text must be at most 500 characters.")]
    public string? Text { get; set; }

    [MaxLength(50, ErrorMessage = "At most 50 excluded ingredients are allowed.")]
    public List<int>? ExcludedIds { get; set; }

    public bool UsePantry { get; set; }
}
