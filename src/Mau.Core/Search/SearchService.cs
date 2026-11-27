using Mau.Core.Catalog;
using Mau.Core.Engine;
using Mau.Core.Feedback;
using Mau.Core.Ranking;
using Mau.Core.Users;

namespace Mau.Core.Search;

/// <summary>Searches the current catalog snapshot.</summary>
public sealed class SearchService : ISearchService
{
    private static readonly IReadOnlySet<int> EmptyDishIds = new HashSet<int>();
    private static readonly IReadOnlySet<int> EmptyIds = new HashSet<int>();

    private readonly CatalogStore _store;
    private readonly IRankingService _ranking;

    public SearchService(CatalogStore store, IRankingService? ranking = null)
    {
        _store = store;
        _ranking = ranking ?? new RankingService(NoDishTotals.Instance, new RankingOptions());
    }

    public SearchResult Search(SearchRequest request)
    {
        var snapshot = _store.Current;
        var parse = snapshot.Recognizer.Parse(request.Text);
        var excluded = new HashSet<int>(request.ExcludedIds ?? Array.Empty<int>());
        IReadOnlySet<int> pantryIds = request.PantryIds is null
            ? EmptyIds
            : request.PantryIds.ToHashSet();

        var tokens = parse.Tokens
            .Select(token => CreateToken(token, snapshot, excluded))
            .ToList();

        var includeIds = tokens
            .Where(token => token.IngredientId is not null && !token.IsExcluded)
            .Select(token => token.IngredientId!.Value)
            .ToHashSet();

        // A never-list ingredient the user typed on purpose counts for this search.
        var neverIds = (request.NeverIds ?? Array.Empty<int>()).ToHashSet();
        var neverOverridden = neverIds.Where(includeIds.Contains).ToList();
        neverIds.ExceptWith(includeIds);
        var neverHidden = 0;
        var neverBlocking = new HashSet<int>();

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

        var toRank = new List<RankCandidate>();
        var preferences = request.Preferences ?? UserPreferences.None;

        foreach (var dishId in candidates)
        {
            var dish = snapshot.DishesById[dishId];
            if (dish.Ingredients.Any(row => excluded.Contains(row.IngredientId)))
            {
                continue;
            }

            // Optional ingredients can be left out, so only required ones hide a dish.
            var blocking = dish.Ingredients
                .Where(row => !row.IsOptional && neverIds.Contains(row.IngredientId))
                .Select(row => row.IngredientId)
                .ToList();
            if (blocking.Count > 0)
            {
                neverHidden++;
                neverBlocking.UnionWith(blocking);
                continue;
            }

            var stillNeeded = dish.Ingredients
                .Where(row => !row.IsOptional && !row.IsStaple && !includeIds.Contains(row.IngredientId)
                    && !pantryIds.Contains(row.IngredientId))
                .Select(row => row.Name)
                .ToList();

            var match = new DishMatch(
                dish.Id,
                dish.Name,
                dish.CookingMethod,
                dish.CookTimeMinutes,
                stillNeeded,
                OverTimeLimit: dish.CookTimeMinutes > preferences.MaxCookTimeMinutes);
            var matchedProtein = preferences.PreferredProteinId is int proteinId &&
                dish.MainProteinId == proteinId
                    ? snapshot.IngredientsById[proteinId].CanonicalName
                    : null;
            var matchedMethod = dish.CookingMethod == preferences.PreferredMethod;
            match = match with { MatchedProtein = matchedProtein, MatchedMethod = matchedMethod };
            var preferenceMatches = (matchedProtein is null ? 0 : 1) + (matchedMethod ? 1 : 0);
            toRank.Add(new RankCandidate(
                match, dish.Ingredients.Count(row => !row.IsOptional), preferenceMatches));
        }

        // Dishes over the time limit stay visible, but only after every dish within it.
        var ranked = _ranking.Rank(toRank)
            .OrderBy(match => match.OverTimeLimit)
            .ToList();

        return new SearchResult(tokens, ranked, ranked.Count(match => match.OverTimeLimit))
        {
            NeverListHiddenCount = neverHidden,
            NeverListBlocking = NamesOf(snapshot, neverBlocking),
            NeverListOverridden = NamesOf(snapshot, neverOverridden),
        };
    }

    private static IReadOnlyList<string> NamesOf(CatalogSnapshot snapshot, IEnumerable<int> ids) =>
        ids.Select(id => snapshot.IngredientsById[id].CanonicalName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
