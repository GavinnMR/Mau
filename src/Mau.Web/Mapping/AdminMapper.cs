using System.Globalization;
using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mau.Web.Mapping;

/// <summary>Maps admin forms, lists and service results.</summary>
public static class AdminMapper
{
    public static IngredientInput ToIngredientInput(IngredientFormViewModel form) =>
        new(
            form.Name,
            form.Category,
            form.IsStaple,
            form.Synonyms
                .Select(row => new SynonymInput(row.Name, row.Language))
                .ToList());

    public static IngredientFormViewModel FromIngredient(CatalogIngredient ingredient)
    {
        var form = new IngredientFormViewModel
        {
            Id = ingredient.Id,
            Name = ingredient.CanonicalName,
            Category = ingredient.Category,
            IsStaple = ingredient.IsStaple,
            Synonyms = ingredient.SynonymItems
                .Select(synonym => new SynonymFormRow
                {
                    Name = synonym.Name,
                    Language = synonym.Language,
                })
                .ToList(),
        };

        PadIngredientRows(form);
        return form;
    }

    public static void PadIngredientRows(IngredientFormViewModel form)
    {
        while (form.Synonyms.Count(row => string.IsNullOrWhiteSpace(row.Name)) < 3)
        {
            form.Synonyms.Add(new SynonymFormRow());
        }
    }

    public static IngredientListViewModel ToIngredientListViewModel(
        CatalogSnapshot catalog, string? query)
    {
        var cleanedQuery = Clean(query);

        var rows = catalog.Ingredients
            .Where(ingredient => Matches(ingredient, cleanedQuery))
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new IngredientRowViewModel(
                ingredient.Id,
                ingredient.CanonicalName,
                ingredient.Category.ToString(),
                ingredient.IsStaple,
                string.Join(", ", ingredient.SynonymItems.Select(synonym => synonym.Name)),
                catalog.DishIdsByIngredientId.TryGetValue(ingredient.Id, out var dishIds)
                    ? dishIds.Count
                    : 0))
            .ToList();

        return new IngredientListViewModel(cleanedQuery, rows);
    }

    public static DishInput ToDishInput(DishFormViewModel form)
    {
        var steps = (form.Steps ?? "")
            .Replace("\r", "")
            .Split('\n')
            .Select(step => step.Trim())
            .Where(step => step.Length > 0)
            .ToArray();

        return new DishInput(
            form.Name,
            form.Description,
            form.CookingMethod,
            form.CookTimeMinutes ?? 0,
            steps,
            form.Ingredients
                .Select(row => new DishIngredientInput(
                    row.IngredientId, row.Quantity, row.Unit, row.IsOptional))
                .ToList(),
            form.MainProteinId);
    }

    public static DishFormViewModel FromDish(CatalogDish dish)
    {
        var form = new DishFormViewModel
        {
            Id = dish.Id,
            Name = dish.Name,
            Description = dish.Description,
            CookingMethod = dish.CookingMethod,
            CookTimeMinutes = dish.CookTimeMinutes,
            MainProteinId = dish.MainProteinId,
            Steps = string.Join("\r\n", dish.Steps),
            Ingredients = dish.Ingredients
                .Select(row => new DishIngredientFormRow
                {
                    IngredientId = row.IngredientId,
                    Quantity = row.Quantity,
                    Unit = row.Unit,
                    IsOptional = row.IsOptional,
                })
                .ToList(),
        };

        PadDishRows(form, 3);
        return form;
    }

    public static void PadDishRows(DishFormViewModel form, int minimumBlankRows)
    {
        while (form.Ingredients.Count(row => row.IngredientId is null) < minimumBlankRows)
        {
            form.Ingredients.Add(new DishIngredientFormRow());
        }
    }

    public static IReadOnlyList<SelectListItem> IngredientOptions(
        CatalogSnapshot catalog) =>
        catalog.Ingredients
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new SelectListItem(
                ingredient.CanonicalName,
                ingredient.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

    public static IReadOnlyList<SelectListItem> ProteinOptions(CatalogSnapshot catalog) =>
        catalog.Ingredients
            .Where(ingredient => ingredient.Category == IngredientCategory.Protein)
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new SelectListItem(
                ingredient.CanonicalName,
                ingredient.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

    public static DishListViewModel ToDishListViewModel(
        CatalogSnapshot catalog, string? query)
    {
        var cleanedQuery = Clean(query);

        var rows = catalog.Dishes
            .Where(dish => cleanedQuery.Length == 0 ||
                dish.Name.Contains(cleanedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dish => dish.Name, StringComparer.OrdinalIgnoreCase)
            .Select(dish => new DishRowViewModel(
                dish.Id,
                dish.Name,
                dish.CookingMethod.ToString(),
                dish.CookTimeMinutes,
                dish.Ingredients.Count))
            .ToList();

        return new DishListViewModel(cleanedQuery, rows);
    }

    public static void AddErrors(ModelStateDictionary modelState, AdminResult result)
    {
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.Field, error.Message);
        }
    }

    private static bool Matches(CatalogIngredient ingredient, string query) =>
        query.Length == 0 ||
        ingredient.CanonicalName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        ingredient.SynonymItems.Any(synonym =>
            synonym.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return string.Join(
            " ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
