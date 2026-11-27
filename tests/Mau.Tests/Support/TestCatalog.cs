using Mau.Core.Catalog;
using Mau.Core.Entities;

namespace Mau.Tests.Support;

public static class TestCatalog
{
    public static CatalogSnapshot CreateSnapshot()
    {
        var ingredients = CreateIngredients();
        return CatalogSnapshot.Create(ingredients, CreateDishes(ingredients));
    }

    public static List<Ingredient> CreateIngredients()
    {
        return new List<Ingredient>
        {
            CreateIngredient(1, "chicken", IngredientCategory.Protein, false, ("manok", SynonymLanguage.Filipino)),
            CreateIngredient(2, "pork", IngredientCategory.Protein, false, ("baboy", SynonymLanguage.Filipino)),
            CreateIngredient(3, "soy sauce", IngredientCategory.Seasoning, false, ("toyo", SynonymLanguage.Filipino)),
            CreateIngredient(4, "vinegar", IngredientCategory.Seasoning, false, ("suka", SynonymLanguage.Filipino)),
            CreateIngredient(5, "garlic", IngredientCategory.Aromatic, false, ("bawang", SynonymLanguage.Filipino)),
            CreateIngredient(6, "onion", IngredientCategory.Aromatic, false, ("sibuyas", SynonymLanguage.Filipino)),
            CreateIngredient(7, "bay leaf", IngredientCategory.Aromatic, false, ("laurel", SynonymLanguage.Filipino), ("dahon ng laurel", SynonymLanguage.Filipino)),
            CreateIngredient(8, "salt", IngredientCategory.Seasoning, true, ("asin", SynonymLanguage.Filipino)),
            CreateIngredient(9, "water", IngredientCategory.Liquid, true, ("tubig", SynonymLanguage.Filipino)),
            CreateIngredient(10, "ginger", IngredientCategory.Aromatic, false, ("luya", SynonymLanguage.Filipino)),
        };
    }

    public static List<Dish> CreateDishes(IReadOnlyList<Ingredient> ingredients)
    {
        var byId = ingredients.ToDictionary(i => i.Id);

        var dishes = new List<Dish>
        {
            CreateDish(1, "Chicken adobo", "Chicken braised in soy sauce and vinegar.", CookingMethod.Braised, 45,
                "Brown the chicken.\n\nSimmer until tender.\n",
                Row(byId, 1, 1, 0),
                Row(byId, 1, 3, 1),
                Row(byId, 1, 4, 2),
                Row(byId, 1, 5, 3),
                Row(byId, 1, 7, 4, isOptional: true),
                Row(byId, 1, 9, 5)),
            CreateDish(2, "Pork adobo", "Pork braised in soy sauce and vinegar.", CookingMethod.Braised, 60,
                "Brown the pork.\nSimmer until tender.",
                Row(byId, 2, 2, 0),
                Row(byId, 2, 3, 1),
                Row(byId, 2, 4, 2),
                Row(byId, 2, 5, 3),
                Row(byId, 2, 7, 4, isOptional: true),
                Row(byId, 2, 9, 5)),
            CreateDish(3, "Chicken tinola", "Chicken soup with ginger and onion.", CookingMethod.Soup, 40,
                "Boil the chicken.\nAdd the aromatics.",
                Row(byId, 3, 1, 0),
                Row(byId, 3, 10, 1),
                Row(byId, 3, 6, 2),
                Row(byId, 3, 5, 3),
                Row(byId, 3, 8, 4),
                Row(byId, 3, 9, 5)),
            CreateDish(4, "Garlic fried chicken", "Chicken fried with garlic.", CookingMethod.Fried, 30,
                "Fry the garlic.\nFry the chicken.",
                Row(byId, 4, 1, 0),
                Row(byId, 4, 5, 1),
                Row(byId, 4, 8, 2)),
        };

        // Main proteins: chicken (1) for the chicken dishes, pork (2) for pork adobo.
        foreach (var dish in dishes)
        {
            dish.MainProteinId = dish.Id == 2 ? 2 : 1;
        }

        return dishes;
    }

    private static Ingredient CreateIngredient(
        int id, string canonicalName, IngredientCategory category, bool isStaple,
        params (string Name, SynonymLanguage Language)[] synonyms)
    {
        var ingredient = new Ingredient
        {
            Id = id,
            CanonicalName = canonicalName,
            Category = category,
            IsStaple = isStaple,
        };

        foreach (var (name, language) in synonyms)
        {
            ingredient.Synonyms.Add(new IngredientSynonym
            {
                IngredientId = id,
                Ingredient = ingredient,
                Name = name,
                Language = language,
            });
        }

        return ingredient;
    }

    private static Dish CreateDish(
        int id, string name, string description, CookingMethod cookingMethod, int cookTimeMinutes,
        string steps, params DishIngredient[] ingredients)
    {
        return new Dish
        {
            Id = id,
            Name = name,
            Description = description,
            CookingMethod = cookingMethod,
            CookTimeMinutes = cookTimeMinutes,
            Steps = steps,
            Ingredients = ingredients.ToList(),
        };
    }

    private static DishIngredient Row(
        IReadOnlyDictionary<int, Ingredient> byId, int dishId, int ingredientId, int position,
        bool isOptional = false)
    {
        return new DishIngredient
        {
            DishId = dishId,
            IngredientId = ingredientId,
            Ingredient = byId[ingredientId],
            Position = position,
            IsOptional = isOptional,
        };
    }
}
