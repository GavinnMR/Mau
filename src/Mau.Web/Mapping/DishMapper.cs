using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Web.Api;

namespace Mau.Web.Mapping;

/// <summary>Maps Core results to API DTOs.</summary>
public static class DishMapper
{
    public static DishDetailsDto ToDto(CatalogDish dish) =>
        new(
            dish.Id,
            dish.Name,
            dish.Description,
            dish.CookingMethod,
            dish.CookTimeMinutes,
            dish.Ingredients
                .Select(row => new DishIngredientDto(
                    row.IngredientId,
                    row.Name,
                    row.Quantity,
                    row.Unit,
                    row.IsOptional,
                    row.IsStaple))
                .ToList(),
            dish.Steps);

    public static SearchResponseDto ToDto(SearchResult result) =>
        new(
            result.Tokens
                .Select(token => new TokenDto(
                    token.Index,
                    token.Raw,
                    token.IngredientId,
                    token.IngredientName,
                    token.IsExcluded,
                    Array.Empty<string>()))
                .ToList(),
            result.Dishes
                .Select(dish => new DishCardDto(
                    dish.DishId,
                    dish.Name,
                    dish.CookingMethod,
                    dish.CookTimeMinutes,
                    dish.StillNeeded,
                    dish.MissingCount))
                .ToList());

    public static IReadOnlyList<HintDto> ToDto(IEnumerable<IngredientHint> hints) =>
        hints
            .Select(hint => new HintDto(hint.Text, hint.IngredientName))
            .ToList();
}
