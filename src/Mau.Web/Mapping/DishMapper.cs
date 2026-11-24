using System.Globalization;
using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Web.Api;
using Mau.Web.ViewModels;

namespace Mau.Web.Mapping;

/// <summary>Maps Core results to API DTOs and page view models.</summary>
public static class DishMapper
{
    public static DishDetailsViewModel ToViewModel(
        CatalogDish dish, bool isAuthenticated = false, bool isFavorite = false) =>
        new(
            dish.Id,
            dish.Name,
            dish.Description,
            dish.CookingMethod.ToString(),
            dish.CookTimeMinutes,
            dish.Ingredients
                .Select(row => new DishIngredientViewModel(
                    row.Name,
                    AmountText(row.Quantity, row.Unit),
                    row.IsOptional,
                    row.IsStaple))
                .ToList(),
            dish.Steps,
            isAuthenticated,
            isFavorite);

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

    public static SearchResponseDto ToDto(SearchResult result, bool pantryApplied) =>
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
                .ToList(),
            pantryApplied);

    public static IReadOnlyList<HintDto> ToDto(IEnumerable<IngredientHint> hints) =>
        hints
            .Select(hint => new HintDto(hint.Text, hint.IngredientName))
            .ToList();

    private static string AmountText(decimal? quantity, string? unit)
    {
        if (quantity is null)
        {
            return "to taste";
        }

        var amount = quantity.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unit) ? amount : $"{amount} {unit}";
    }
}
