using System.Globalization;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mau.Web.Mapping;

/// <summary>Builds profile page view models from the catalog and a user's ids.</summary>
public static class ProfileMapper
{
    public static PreferencesViewModel ToPreferencesViewModel(
        CatalogSnapshot catalog, UserPreferences preferences, IReadOnlySet<int> neverIds)
    {
        var form = new PreferencesViewModel
        {
            PreferredProteinId = preferences.PreferredProteinId,
            PreferredMethod = preferences.PreferredMethod,
            MaxCookTimeMinutes = preferences.MaxCookTimeMinutes,
            NeverIds = neverIds.ToList(),
        };
        FillOptions(form, catalog);
        return form;
    }

    /// <summary>Fills the dropdown and checklist options, keeping the form's ticked ingredients.</summary>
    public static void FillOptions(PreferencesViewModel form, CatalogSnapshot catalog)
    {
        form.ProteinOptions = ProteinOptions(catalog);
        var selected = form.NeverIds.ToHashSet();
        form.NeverGroups = Enum.GetValues<IngredientCategory>()
            .Select(category => new PantryGroupViewModel(
                GetTitle(category),
                catalog.Ingredients
                    .Where(ingredient => ingredient.Category == category)
                    .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
                    .Select(ingredient => new PantryOptionViewModel(
                        ingredient.Id, ingredient.CanonicalName, selected.Contains(ingredient.Id)))
                    .ToList()))
            .Where(group => group.Options.Count > 0)
            .ToList();
    }

    public static UserPreferences ToPreferences(PreferencesViewModel form) =>
        new(form.PreferredProteinId, form.PreferredMethod, form.MaxCookTimeMinutes);

    public static IReadOnlyList<SelectListItem> ProteinOptions(CatalogSnapshot catalog) =>
        catalog.Ingredients
            .Where(ingredient => ingredient.Category == IngredientCategory.Protein)
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new SelectListItem(
                ingredient.CanonicalName,
                ingredient.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

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
                DateTime.SpecifyKind(entry.SearchedAtUtc, DateTimeKind.Utc)
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
