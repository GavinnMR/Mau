using System.Globalization;
using Mau.Core.Catalog;
using Mau.Core.Feedback;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Api;
using Mau.Web.ViewModels;

namespace Mau.Web.Mapping;

/// <summary>Maps Core results to API DTOs and page view models.</summary>
public static class DishMapper
{
    public static DishDetailsViewModel ToViewModel(
        CatalogDish dish, bool isAuthenticated = false, bool isFavorite = false,
        DishFeedbackViewModel? feedback = null) =>
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
            isFavorite,
            feedback);

    public static DishFeedbackViewModel ToFeedbackViewModel(
        DishTotals totals, UserDishFeedback user,
        string cookedState = "needs-search", string? searchToken = null) =>
        new(
            totals.CookedCount,
            totals.UpCount,
            totals.DownCount,
            user.CookedCount,
            user.Vote switch { Vote.Up => "up", Vote.Down => "down", _ => "none" },
            cookedState,
            searchToken);

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

    public static SearchResponseDto ToDto(
        SearchResult result, bool pantryApplied, string? searchToken = null,
        UserPreferences? preferences = null) =>
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
                    dish.MissingCount,
                    Math.Round(dish.Score, 4),
                    dish.OverTimeLimit)
                {
                    PreferenceBadges = PreferenceBadges(dish),
                })
                .ToList(),
            pantryApplied,
            searchToken,
            preferences is { IsEmpty: false },
            result.OverTimeLimitCount,
            preferences?.MaxCookTimeMinutes)
        {
            NeverListHiddenCount = result.NeverListHiddenCount,
            NeverListBlocking = result.NeverListBlocking,
            NeverListOverridden = result.NeverListOverridden,
        };

    private static IReadOnlyList<string> PreferenceBadges(DishMatch dish)
    {
        var badges = new List<string>();
        if (dish.MatchedProtein is string protein)
        {
            badges.Add($"{protein}, your protein");
        }

        if (dish.MatchedMethod)
        {
            badges.Add($"{dish.CookingMethod}, your method");
        }

        return badges;
    }

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
