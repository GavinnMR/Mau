using System.Globalization;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.ViewModels;

namespace Mau.Web.Mapping;

/// <summary>Builds profile page view models from the catalog and a user's ids.</summary>
public static class ProfileMapper
{
    public static PantryViewModel ToPantryViewModel(
        CatalogSnapshot catalog, IReadOnlySet<int> selectedIds)
    {
        var groups = new List<PantryGroupViewModel>();

        foreach (var category in Enum.GetValues<IngredientCategory>())
        {
            var options = catalog.Ingredients
                .Where(ingredient => !ingredient.IsStaple && ingredient.Category == category)
                .OrderBy(
                    ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
                .Select(ingredient => new PantryOptionViewModel(
                    ingredient.Id,
                    ingredient.CanonicalName,
                    selectedIds.Contains(ingredient.Id)))
                .ToList();

            if (options.Count == 0)
            {
                continue;
            }

            groups.Add(new PantryGroupViewModel(GetTitle(category), options));
        }

        return new PantryViewModel(groups, selectedIds.Count);
    }

    public static FavoritesViewModel ToFavoritesViewModel(
        IReadOnlyList<int> dishIds, ISearchService searchService)
    {
        var items = new List<FavoriteItemViewModel>();

        foreach (var dishId in dishIds)
        {
            var dish = searchService.GetDish(dishId);
            if (dish is null)
            {
                continue;
            }

            items.Add(new FavoriteItemViewModel(
                dish.Id, dish.Name, dish.CookingMethod.ToString(), dish.CookTimeMinutes));
        }

        return new FavoritesViewModel(items);
    }

    public static HistoryViewModel ToHistoryViewModel(
        IReadOnlyList<SearchLogEntry> entries, CatalogSnapshot catalog)
    {
        var items = new List<HistoryItemViewModel>();

        foreach (var entry in entries)
        {
            var names = entry.IngredientIds
                .Select(id => catalog.IngredientsById.TryGetValue(id, out var ingredient)
                    ? ingredient.CanonicalName
                    : null)
                .Where(name => name is not null)
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                continue;
            }

            items.Add(new HistoryItemViewModel(
                entry.Id,
                entry.SearchedAtUtc
                    .ToLocalTime()
                    .ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture),
                entry.ResultCount,
                names,
                string.Join(",", names)));
        }

        return new HistoryViewModel(items);
    }

    private static string GetTitle(IngredientCategory category) => category switch
    {
        IngredientCategory.Protein => "Proteins",
        IngredientCategory.Vegetable => "Vegetables",
        IngredientCategory.Aromatic => "Aromatics",
        IngredientCategory.Seasoning => "Seasonings and sauces",
        IngredientCategory.Liquid => "Liquids",
        IngredientCategory.Legume => "Legumes",
        _ => "Other",
    };
}
