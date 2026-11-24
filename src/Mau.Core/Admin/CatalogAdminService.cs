using Mau.Core.Catalog;
using Mau.Core.Engine;

namespace Mau.Core.Admin;

/// <summary>Validates and applies catalog changes from the admin pages.</summary>
public sealed class CatalogAdminService : ICatalogAdminService
{
    private readonly ICatalogAdminStore _store;
    private readonly CatalogStore _catalog;

    public CatalogAdminService(ICatalogAdminStore store, CatalogStore catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    public async Task<AdminResult> CreateIngredientAsync(
        IngredientInput input, CancellationToken ct = default)
    {
        var errors = ValidateIngredient(_catalog.Current, input, excludeId: null);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors.ToArray());
        }

        int id;
        try
        {
            id = await _store.AddIngredientAsync(CleanIngredient(input), ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("Name", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> UpdateIngredientAsync(
        int id, IngredientInput input, CancellationToken ct = default)
    {
        if (!_catalog.Current.IngredientsById.ContainsKey(id))
        {
            return AdminResult.Fail(new ValidationError("", "Ingredient not found."));
        }

        var errors = ValidateIngredient(_catalog.Current, input, excludeId: id);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors.ToArray());
        }

        try
        {
            await _store.UpdateIngredientAsync(id, CleanIngredient(input), ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("Name", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> DeleteIngredientAsync(
        int id, CancellationToken ct = default)
    {
        var snapshot = _catalog.Current;
        if (!snapshot.IngredientsById.TryGetValue(id, out var ingredient))
        {
            return AdminResult.Fail(new ValidationError("", "Ingredient not found."));
        }

        if (snapshot.DishIdsByIngredientId.TryGetValue(id, out var dishIds) &&
            dishIds.Count > 0)
        {
            return AdminResult.Fail(
                new ValidationError("", CreateDeleteMessage(snapshot, ingredient, dishIds)));
        }

        try
        {
            await _store.DeleteIngredientAsync(id, ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> CreateDishAsync(
        DishInput input, CancellationToken ct = default)
    {
        var errors = ValidateDish(_catalog.Current, input, excludeId: null);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors.ToArray());
        }

        int id;
        try
        {
            id = await _store.AddDishAsync(CleanDish(input), ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("Name", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> UpdateDishAsync(
        int id, DishInput input, CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(id))
        {
            return AdminResult.Fail(new ValidationError("", "Dish not found."));
        }

        var errors = ValidateDish(_catalog.Current, input, excludeId: id);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors.ToArray());
        }

        try
        {
            await _store.UpdateDishAsync(id, CleanDish(input), ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("Name", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> DeleteDishAsync(int id, CancellationToken ct = default)
    {
        if (!_catalog.Current.DishesById.ContainsKey(id))
        {
            return AdminResult.Fail(new ValidationError("", "Dish not found."));
        }

        try
        {
            await _store.DeleteDishAsync(id, ct);
        }
        catch (CatalogConflictException e)
        {
            return AdminResult.Fail(new ValidationError("", e.Message));
        }

        await _catalog.ReloadAsync(ct);
        return AdminResult.Ok(id);
    }

    private static string CreateDeleteMessage(
        CatalogSnapshot snapshot, CatalogIngredient ingredient, IReadOnlySet<int> dishIds)
    {
        var names = dishIds
            .Select(dishId => snapshot.DishesById[dishId].Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var listed = string.Join(", ", names.Take(5));
        if (names.Count > 5)
        {
            listed += $" and {names.Count - 5} more";
        }

        var noun = names.Count == 1 ? "dish" : "dishes";
        return $"Cannot delete “{ingredient.CanonicalName}”: it is used by {names.Count} {noun}: {listed}.";
    }

    private static List<ValidationError> ValidateDish(
        CatalogSnapshot snapshot, DishInput input, int? excludeId)
    {
        var errors = new List<ValidationError>();
        var name = Clean(input.Name);
        var description = Clean(input.Description);
        var steps = CleanSteps(input.Steps);

        if (name.Length == 0)
        {
            errors.Add(new ValidationError("Name", "Name is required."));
        }
        else if (name.Length > 120)
        {
            errors.Add(new ValidationError(
                "Name", "Name must be at most 120 characters."));
        }
        else
        {
            var nameKey = TextNormalizer.Normalize(name);
            if (snapshot.Dishes.Any(dish =>
                dish.Id != excludeId &&
                TextNormalizer.Normalize(dish.Name) == nameKey))
            {
                errors.Add(new ValidationError(
                    "Name", "A dish with that name already exists."));
            }
        }

        if (description.Length == 0)
        {
            errors.Add(new ValidationError("Description", "Description is required."));
        }
        else if (description.Length > 500)
        {
            errors.Add(new ValidationError(
                "Description", "Description must be at most 500 characters."));
        }

        if (!Enum.IsDefined(input.CookingMethod))
        {
            errors.Add(new ValidationError(
                "CookingMethod", "Choose a cooking method."));
        }

        if (input.CookTimeMinutes < 1 || input.CookTimeMinutes > 600)
        {
            errors.Add(new ValidationError(
                "CookTimeMinutes", "Cooking time must be between 1 and 600 minutes."));
        }

        if (steps.Count == 0)
        {
            errors.Add(new ValidationError("Steps", "Add at least one step."));
        }
        else
        {
            if (steps.Any(step => step.Length > 500))
            {
                errors.Add(new ValidationError(
                    "Steps", "Each step must be at most 500 characters."));
            }

            if (string.Join("\n", steps).Length > 4000)
            {
                errors.Add(new ValidationError(
                    "Steps", "Steps are too long (4000 characters in total)."));
            }
        }

        var chosenRows = input.Ingredients
            .Where(row => row.IngredientId is not null)
            .ToList();

        if (chosenRows.Count > 40)
        {
            errors.Add(new ValidationError(
                "Ingredients", "A dish can have at most 40 ingredients."));
        }

        if (!chosenRows.Any(row =>
            row.IngredientId is int id &&
            !row.IsOptional &&
            snapshot.IngredientsById.ContainsKey(id)))
        {
            errors.Add(new ValidationError(
                "Ingredients", "A dish needs at least one required ingredient."));
        }

        var seenIngredientIds = new HashSet<int>();
        for (var i = 0; i < input.Ingredients.Count; i++)
        {
            var row = input.Ingredients[i];
            if (row.IngredientId is not int ingredientId)
            {
                continue;
            }

            if (!snapshot.IngredientsById.TryGetValue(ingredientId, out var ingredient))
            {
                errors.Add(new ValidationError(
                    $"Ingredients[{i}].IngredientId",
                    "Choose an existing ingredient."));
            }
            else if (!seenIngredientIds.Add(ingredientId))
            {
                errors.Add(new ValidationError(
                    $"Ingredients[{i}].IngredientId",
                    $"{ingredient.CanonicalName} is listed more than once."));
            }

            if (row.Quantity is decimal quantity)
            {
                if (quantity <= 0)
                {
                    errors.Add(new ValidationError(
                        $"Ingredients[{i}].Quantity",
                        "Quantity must be greater than 0."));
                }
                else if (quantity > 100000)
                {
                    errors.Add(new ValidationError(
                        $"Ingredients[{i}].Quantity",
                        "Quantity must be at most 100000."));
                }
            }

            var unit = Clean(row.Unit);
            if (unit.Length > 20)
            {
                errors.Add(new ValidationError(
                    $"Ingredients[{i}].Unit", "Unit must be at most 20 characters."));
            }

            if (unit.Length > 0 && row.Quantity is null)
            {
                errors.Add(new ValidationError(
                    $"Ingredients[{i}].Unit", "Enter a quantity or clear the unit."));
            }
        }

        return errors;
    }

    private static DishInput CleanDish(DishInput input)
    {
        var rows = new List<DishIngredientInput>();
        foreach (var row in input.Ingredients)
        {
            if (row.IngredientId is not int ingredientId)
            {
                continue;
            }

            var unit = Clean(row.Unit);
            rows.Add(new DishIngredientInput(
                ingredientId,
                row.Quantity,
                unit.Length == 0 ? null : unit,
                row.IsOptional));
        }

        return new DishInput(
            Clean(input.Name),
            Clean(input.Description),
            input.CookingMethod,
            input.CookTimeMinutes,
            CleanSteps(input.Steps),
            rows);
    }

    private static List<string> CleanSteps(IReadOnlyList<string> steps)
    {
        var cleaned = new List<string>();
        foreach (var step in steps)
        {
            var trimmed = step.Trim();
            if (trimmed.Length > 0)
            {
                cleaned.Add(trimmed);
            }
        }

        return cleaned;
    }

    private static List<ValidationError> ValidateIngredient(
        CatalogSnapshot snapshot, IngredientInput input, int? excludeId)
    {
        var errors = new List<ValidationError>();
        var name = Clean(input.Name);
        var nameIsUsable = name.Length > 0;
        var namePassedTextRules =
            nameIsUsable && name.Length <= 100 && !name.Contains(',');

        if (!nameIsUsable)
        {
            errors.Add(new ValidationError("Name", "Name is required."));
        }
        else
        {
            if (name.Length > 100)
            {
                errors.Add(new ValidationError(
                    "Name", "Name must be at most 100 characters."));
            }

            if (name.Contains(','))
            {
                errors.Add(new ValidationError("Name", "Name cannot contain a comma."));
            }
        }

        if (!Enum.IsDefined(input.Category))
        {
            errors.Add(new ValidationError("Category", "Choose a category."));
        }

        var synonyms = input.Synonyms;
        if (synonyms.Count(synonym => Clean(synonym.Name).Length > 0) > 20)
        {
            errors.Add(new ValidationError("Synonyms", "At most 20 synonyms."));
        }

        var others = BuildKeyLookup(snapshot, excludeId);
        var earlierKeys = new HashSet<string>(StringComparer.Ordinal);

        if (namePassedTextRules)
        {
            var key = TextNormalizer.Normalize(name);
            if (others.TryGetValue(key, out var otherName))
            {
                errors.Add(new ValidationError(
                    "Name", $"That name is already used by {otherName}."));
            }

            earlierKeys.Add(key);
        }

        for (var i = 0; i < synonyms.Count; i++)
        {
            var synonym = synonyms[i];
            var synonymName = Clean(synonym.Name);
            if (synonymName.Length == 0)
            {
                continue;
            }

            if (synonymName.Length > 100)
            {
                errors.Add(new ValidationError(
                    $"Synonyms[{i}].Name", "Synonym must be at most 100 characters."));
            }

            if (synonymName.Contains(','))
            {
                errors.Add(new ValidationError(
                    $"Synonyms[{i}].Name", "Synonym cannot contain a comma."));
            }

            if (!Enum.IsDefined(synonym.Language))
            {
                errors.Add(new ValidationError(
                    $"Synonyms[{i}].Language", "Choose a language."));
            }

            if (synonymName.Length > 100 || synonymName.Contains(','))
            {
                continue;
            }

            var key = TextNormalizer.Normalize(synonymName);
            if (others.TryGetValue(key, out var otherName))
            {
                errors.Add(new ValidationError(
                    $"Synonyms[{i}].Name",
                    $"“{synonymName}” is already used by {otherName}."));
            }
            else if (earlierKeys.Contains(key))
            {
                errors.Add(new ValidationError(
                    $"Synonyms[{i}].Name",
                    $"“{synonymName}” is listed more than once."));
            }

            earlierKeys.Add(key);
        }

        return errors;
    }

    private static Dictionary<string, string> BuildKeyLookup(
        CatalogSnapshot snapshot, int? excludeId)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var ingredient in snapshot.Ingredients)
        {
            if (ingredient.Id == excludeId)
            {
                continue;
            }

            AddKey(lookup, ingredient.CanonicalName, ingredient.CanonicalName);
            foreach (var synonym in ingredient.Synonyms)
            {
                AddKey(lookup, synonym, ingredient.CanonicalName);
            }
        }

        return lookup;
    }

    private static void AddKey(
        Dictionary<string, string> lookup, string text, string canonicalName)
    {
        var key = TextNormalizer.Normalize(text);
        if (!lookup.ContainsKey(key))
        {
            lookup[key] = canonicalName;
        }
    }

    private static IngredientInput CleanIngredient(IngredientInput input)
    {
        var synonyms = new List<SynonymInput>();
        foreach (var synonym in input.Synonyms)
        {
            var name = Clean(synonym.Name);
            if (name.Length > 0)
            {
                synonyms.Add(new SynonymInput(name, synonym.Language));
            }
        }

        return new IngredientInput(
            Clean(input.Name), input.Category, input.IsStaple, synonyms);
    }

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
