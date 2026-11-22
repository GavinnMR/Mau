using Mau.Core.Catalog;
using Mau.Core.Engine;

namespace Mau.Core.Search;

/// <summary>Searches the current catalog snapshot.</summary>
public sealed class SearchService : ISearchService
{
    private static readonly IReadOnlySet<int> EmptyDishIds = new HashSet<int>();

    private readonly CatalogStore _store;

    public SearchService(CatalogStore store)
    {
        _store = store;
    }

    public SearchResult Search(SearchRequest request)
    {
        var snapshot = _store.Current;
        var parse = snapshot.Recognizer.Parse(request.Text);
        var excluded = new HashSet<int>(request.ExcludedIds ?? Array.Empty<int>());

        var tokens = parse.Tokens
            .Select(token => CreateToken(token, snapshot, excluded))
            .ToList();

        var includeIds = tokens
            .Where(token => token.IngredientId is not null && !token.IsExcluded)
            .Select(token => token.IngredientId!.Value)
            .ToHashSet();

        if (includeIds.Count == 0)
        {
            return new SearchResult(tokens, Array.Empty<DishMatch>());
        }

        HashSet<int>? candidates = null;
        foreach (var ingredientId in includeIds)
        {
            var dishIds = snapshot.DishIdsByIngredientId.TryGetValue(ingredientId, out var ids)
                ? ids
                : EmptyDishIds;

            if (candidates is null)
            {
                candidates = new HashSet<int>(dishIds);
            }
            else
            {
                candidates.IntersectWith(dishIds);
            }

            if (candidates.Count == 0)
            {
                break;
            }
        }

        if (candidates is null)
        {
            return new SearchResult(tokens, Array.Empty<DishMatch>());
        }

        var matches = new List<DishMatch>();
        foreach (var dishId in candidates)
        {
            var dish = snapshot.DishesById[dishId];
            if (dish.Ingredients.Any(row => excluded.Contains(row.IngredientId)))
            {
                continue;
            }

            var stillNeeded = dish.Ingredients
                .Where(row => !row.IsOptional && !row.IsStaple && !includeIds.Contains(row.IngredientId))
                .Select(row => row.Name)
                .ToList();

            matches.Add(new DishMatch(
                dish.Id,
                dish.Name,
                dish.CookingMethod,
                dish.CookTimeMinutes,
                stillNeeded));
        }

        var ordered = matches
            .OrderBy(match => match.MissingCount)
            .ThenBy(match => match.CookTimeMinutes)
            .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SearchResult(tokens, ordered);
    }

    public IReadOnlyList<IngredientHint> GetHints(string? prefix)
    {
        var snapshot = _store.Current;
        return snapshot.Recognizer.GetHints(prefix)
            .Select(hint => new IngredientHint(
                hint.Text,
                hint.IngredientId,
                snapshot.IngredientsById[hint.IngredientId].CanonicalName))
            .ToList();
    }

    public CatalogDish? GetDish(int id) => _store.Current.DishesById.GetValueOrDefault(id);

    private static SearchToken CreateToken(
        ParsedToken token, CatalogSnapshot snapshot, IReadOnlySet<int> excluded)
    {
        var ingredientName =
            token.IngredientId is int id && snapshot.IngredientsById.TryGetValue(id, out var ingredient)
                ? ingredient.CanonicalName
                : null;

        return new SearchToken(
            token.Index,
            token.Raw,
            token.IngredientId,
            ingredientName,
            token.IngredientId is int tokenId && excluded.Contains(tokenId));
    }
}
