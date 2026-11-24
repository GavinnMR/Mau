using Mau.Core.Engine;
using Mau.Core.Entities;

namespace Mau.Core.Catalog;

/// <summary>An immutable, fully loaded view of the catalog.</summary>
public sealed class CatalogSnapshot
{
    private CatalogSnapshot(
        IReadOnlyList<CatalogIngredient> ingredients,
        IReadOnlyList<CatalogDish> dishes,
        IReadOnlyDictionary<int, CatalogIngredient> ingredientsById,
        IReadOnlyDictionary<int, CatalogDish> dishesById,
        IReadOnlyDictionary<int, IReadOnlySet<int>> dishIdsByIngredientId,
        IngredientRecognizer recognizer)
    {
        Ingredients = ingredients;
        Dishes = dishes;
        IngredientsById = ingredientsById;
        DishesById = dishesById;
        DishIdsByIngredientId = dishIdsByIngredientId;
        Recognizer = recognizer;
    }

    public IReadOnlyList<CatalogIngredient> Ingredients { get; }
    public IReadOnlyList<CatalogDish> Dishes { get; }
    public IReadOnlyDictionary<int, CatalogIngredient> IngredientsById { get; }
    public IReadOnlyDictionary<int, CatalogDish> DishesById { get; }
    public IReadOnlyDictionary<int, IReadOnlySet<int>> DishIdsByIngredientId { get; }
    public IngredientRecognizer Recognizer { get; }

    public static CatalogSnapshot Create(IEnumerable<Ingredient> ingredients, IEnumerable<Dish> dishes)
    {
        var ingredientList = ingredients.ToList();
        var dishList = dishes.ToList();
        var ingredientsById = ingredientList.ToDictionary(i => i.Id);

        var lexicon = new List<LexiconEntry>();
        foreach (var ingredient in ingredientList)
        {
            AddLexiconEntry(lexicon, ingredient.CanonicalName, ingredient.Id);
            foreach (var synonym in ingredient.Synonyms)
            {
                AddLexiconEntry(lexicon, synonym.Name, ingredient.Id);
            }
        }

        var recognizer = new IngredientRecognizer(lexicon);

        var catalogDishes = dishList
            .Select(dish => CreateDish(dish, ingredientsById))
            .OrderBy(dish => dish.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalogIngredients = ingredientList
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new CatalogIngredient(
                ingredient.Id,
                ingredient.CanonicalName,
                ingredient.Category,
                ingredient.IsStaple,
                ingredient.Synonyms.Select(synonym => synonym.Name).ToList())
            {
                SynonymItems = ingredient.Synonyms
                    .Select(synonym => new CatalogSynonym(synonym.Name, synonym.Language))
                    .OrderBy(synonym => synonym.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .ToList();

        var dishIdsByIngredientId = new Dictionary<int, HashSet<int>>();
        foreach (var dish in catalogDishes)
        {
            foreach (var row in dish.Ingredients)
            {
                if (!dishIdsByIngredientId.TryGetValue(row.IngredientId, out var dishIds))
                {
                    dishIds = new HashSet<int>();
                    dishIdsByIngredientId[row.IngredientId] = dishIds;
                }

                dishIds.Add(dish.Id);
            }
        }

        var readOnlyDishIds = dishIdsByIngredientId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<int>)pair.Value);

        return new CatalogSnapshot(
            catalogIngredients,
            catalogDishes,
            catalogIngredients.ToDictionary(ingredient => ingredient.Id),
            catalogDishes.ToDictionary(dish => dish.Id),
            readOnlyDishIds,
            recognizer);
    }

    private static void AddLexiconEntry(List<LexiconEntry> entries, string name, int ingredientId)
    {
        entries.Add(new LexiconEntry(TextNormalizer.Normalize(name), ingredientId));
    }

    private static CatalogDish CreateDish(Dish dish, IReadOnlyDictionary<int, Ingredient> ingredientsById)
    {
        var rows = new List<CatalogDishIngredient>();
        foreach (var row in dish.Ingredients.OrderBy(row => row.Position))
        {
            if (!ingredientsById.TryGetValue(row.IngredientId, out var ingredient))
            {
                throw new CatalogException(
                    $"Dish '{dish.Name}' references unknown ingredient id {row.IngredientId}.");
            }

            rows.Add(new CatalogDishIngredient(
                ingredient.Id,
                ingredient.CanonicalName,
                row.Quantity,
                row.Unit,
                row.IsOptional,
                ingredient.IsStaple));
        }

        var steps = dish.Steps
            .Split('\n')
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList();

        return new CatalogDish(
            dish.Id,
            dish.Name,
            dish.Description,
            dish.CookingMethod,
            dish.CookTimeMinutes,
            steps,
            rows);
    }
}
