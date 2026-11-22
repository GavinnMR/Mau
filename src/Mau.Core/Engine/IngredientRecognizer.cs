namespace Mau.Core.Engine;

/// <summary>Recognizes committed ingredient tokens and pending prefixes from typed text.</summary>
public sealed class IngredientRecognizer
{
    public const int MaxHints = 8;

    private readonly IngredientTrie _trie = new();

    public IngredientRecognizer(IEnumerable<LexiconEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Key.Length == 0)
            {
                continue;
            }

            _trie.Add(entry.Key, entry.IngredientId);
        }
    }

    public ParseResult Parse(string? text)
    {
        text ??= "";
        text = text.Replace("\r", "");

        var lastDelimiter = text.LastIndexOfAny(new[] { ',', '\n' });
        string committed;
        string pending;
        if (lastDelimiter < 0)
        {
            committed = "";
            pending = text;
        }
        else
        {
            committed = text[..lastDelimiter];
            pending = text[(lastDelimiter + 1)..];
        }

        var tokens = new List<ParsedToken>();
        foreach (var piece in committed.Split(new[] { ',', '\n' }))
        {
            var raw = piece.Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            var normalized = TextNormalizer.Normalize(raw);
            var ingredientId = _trie.TryGetExact(normalized, out var id) ? id : (int?)null;
            tokens.Add(new ParsedToken(tokens.Count, raw, normalized, ingredientId));
        }

        var pendingPrefix = TextNormalizer.Normalize(pending);
        return new ParseResult(tokens, pendingPrefix, GetHints(pendingPrefix));
    }

    public IReadOnlyList<LexiconHint> GetHints(string? prefix)
    {
        var normalized = TextNormalizer.Normalize(prefix);
        if (normalized.Length == 0)
        {
            return Array.Empty<LexiconHint>();
        }

        return _trie.GetPrefixMatches(normalized, MaxHints)
            .Select(match => new LexiconHint(match.Text, match.IngredientId))
            .ToList();
    }
}
