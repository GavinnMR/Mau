using Mau.Core.Catalog;

namespace Mau.Core.Engine;

/// <summary>A prefix tree over normalized ingredient keys.</summary>
public sealed class IngredientTrie
{
    private readonly Node _root = new();

    public void Add(string key, int ingredientId)
    {
        var node = _root;
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                child = new Node();
                node.Children[ch] = child;
            }

            node = child;
        }

        if (node.IngredientId is int existing && existing != ingredientId)
        {
            throw new CatalogException(
                $"Key '{key}' is already assigned to ingredient {existing} and cannot map to ingredient {ingredientId}.");
        }

        node.IngredientId = ingredientId;
    }

    public bool TryGetExact(string key, out int ingredientId)
    {
        var node = _root;
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out node))
            {
                ingredientId = 0;
                return false;
            }
        }

        if (node.IngredientId is int id)
        {
            ingredientId = id;
            return true;
        }

        ingredientId = 0;
        return false;
    }

    public IReadOnlyList<(string Text, int IngredientId)> GetPrefixMatches(string prefix, int max)
    {
        var matches = new List<(string Text, int IngredientId)>();
        if (prefix.Length == 0 || max <= 0)
        {
            return matches;
        }

        var node = _root;
        foreach (var ch in prefix)
        {
            if (!node.Children.TryGetValue(ch, out node))
            {
                return matches;
            }
        }

        Collect(node, prefix, max, matches);
        return matches;
    }

    private static void Collect(Node node, string text, int max, List<(string Text, int IngredientId)> matches)
    {
        if (matches.Count >= max)
        {
            return;
        }

        if (node.IngredientId is int id)
        {
            matches.Add((text, id));
        }

        foreach (var (ch, child) in node.Children)
        {
            Collect(child, text + ch, max, matches);
        }
    }

    private sealed class Node
    {
        public SortedDictionary<char, Node> Children { get; } = new();
        public int? IngredientId { get; set; }
    }
}
