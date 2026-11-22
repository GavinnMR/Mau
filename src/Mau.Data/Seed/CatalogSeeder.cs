using System.Text.Json;
using System.Text.Json.Serialization;
using Mau.Core.Engine;
using Mau.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data.Seed;

/// <summary>Seeds the catalog from the embedded JSON resource.</summary>
public static class CatalogSeeder
{
    private const string ResourceName = "Mau.Data.Seed.catalog.json";

    public static async Task SeedAsync(MauDbContext context, CancellationToken ct = default)
    {
        var seed = await ReadAsync(ct);
        var ingredientEntities = CreateIngredients(seed.Ingredients);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        context.Ingredients.AddRange(ingredientEntities.Values);
        await context.SaveChangesAsync(ct);

        var dishes = CreateDishes(seed.Dishes, ingredientEntities);
        context.Dishes.AddRange(dishes);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task<CatalogSeed> ReadAsync(CancellationToken ct)
    {
        await using var stream = typeof(CatalogSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        return await JsonSerializer.DeserializeAsync<CatalogSeed>(stream, options, ct)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is empty.");
    }

    private static Dictionary<string, Ingredient> CreateIngredients(IEnumerable<SeedIngredient> seedIngredients)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var ingredients = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedIngredient in seedIngredients)
        {
            AddName(names, seedIngredient.Name, "ingredient");
            foreach (var synonym in seedIngredient.Synonyms)
            {
                AddName(names, synonym.Name, "synonym");
            }

            var ingredient = new Ingredient
            {
                CanonicalName = seedIngredient.Name,
                Category = seedIngredient.Category,
                IsStaple = seedIngredient.IsStaple,
                Synonyms = seedIngredient.Synonyms
                    .Select(synonym => new IngredientSynonym
                    {
                        Name = synonym.Name,
                        Language = synonym.Language,
                    })
                    .ToList(),
            };

            if (!ingredients.TryAdd(seedIngredient.Name, ingredient))
            {
                throw new InvalidOperationException(
                    $"Ingredient '{seedIngredient.Name}' is listed more than once.");
            }
        }

        return ingredients;
    }

    private static List<Dish> CreateDishes(
        IEnumerable<SeedDish> seedDishes, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        var dishes = new List<Dish>();

        foreach (var seedDish in seedDishes)
        {
            var rows = new List<DishIngredient>();
            for (var position = 0; position < seedDish.Ingredients.Count; position++)
            {
                var row = seedDish.Ingredients[position];
                if (!ingredients.TryGetValue(row.Name, out var ingredient))
                {
                    throw new InvalidOperationException(
                        $"Dish '{seedDish.Name}' references unknown ingredient '{row.Name}'.");
                }

                rows.Add(new DishIngredient
                {
                    IngredientId = ingredient.Id,
                    Position = position,
                    Quantity = row.Quantity,
                    Unit = row.Unit,
                    IsOptional = row.IsOptional,
                });
            }

            dishes.Add(new Dish
            {
                Name = seedDish.Name,
                Description = seedDish.Description,
                CookingMethod = seedDish.CookingMethod,
                CookTimeMinutes = seedDish.CookTimeMinutes,
                Steps = string.Join('\n', seedDish.Steps),
                Ingredients = rows,
            });
        }

        return dishes;
    }

    private static void AddName(Dictionary<string, string> names, string name, string kind)
    {
        var key = TextNormalizer.Normalize(name);
        if (!names.TryAdd(key, name))
        {
            throw new InvalidOperationException(
                $"The {kind} '{name}' is already used by '{names[key]}'.");
        }
    }
}
