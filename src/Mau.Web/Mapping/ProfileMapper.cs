using Mau.Core.Catalog;
using Mau.Core.Entities;
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
