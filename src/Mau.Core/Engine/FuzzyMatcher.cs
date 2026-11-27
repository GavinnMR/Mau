namespace Mau.Core.Engine;

/// <summary>A known name close to an unknown word.</summary>
public sealed record FuzzySuggestion(string Text, int IngredientId, int Distance);

/// <summary>
/// Finds known ingredient names close to an unknown word by Levenshtein distance (FR-10).
/// It only suggests; nothing is applied automatically.
/// </summary>
public sealed class FuzzyMatcher
{
    private readonly IReadOnlyList<LexiconEntry> _entries;

    public FuzzyMatcher(IEnumerable<LexiconEntry> entries)
    {
        _entries = entries.ToList();
    }

    /// <summary>Up to MaxSuggestions names, one per ingredient, closest first.</summary>
    public IReadOnlyList<FuzzySuggestion> Suggest(string? word, FuzzyOptions options)
    {
        var key = TextNormalizer.Normalize(word);
        if (key.Length == 0)
        {
            return Array.Empty<FuzzySuggestion>();
        }

        var maxDistance = key.Length >= options.LongWordLength
            ? options.MaxDistanceLongWords
            : options.MaxDistance;

        var best = new Dictionary<int, FuzzySuggestion>();
        foreach (var entry in _entries)
        {
            if (entry.Key == key || Math.Abs(entry.Key.Length - key.Length) > maxDistance)
            {
                continue;
            }

            var distance = Distance(key, entry.Key, maxDistance);
            if (distance > maxDistance)
            {
                continue;
            }

            if (!best.TryGetValue(entry.IngredientId, out var current) ||
                distance < current.Distance ||
                (distance == current.Distance && string.CompareOrdinal(entry.Key, current.Text) < 0))
            {
                best[entry.IngredientId] = new FuzzySuggestion(entry.Key, entry.IngredientId, distance);
            }
        }

        return best.Values
            .OrderBy(suggestion => suggestion.Distance)
            .ThenBy(suggestion => suggestion.Text, StringComparer.Ordinal)
            .Take(options.MaxSuggestions)
            .ToList();
    }

    /// <summary>Levenshtein distance; stops early and returns limit + 1 once it is exceeded.</summary>
    public static int Distance(string a, string b, int limit)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }

            if (rowMin > limit)
            {
                return limit + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
