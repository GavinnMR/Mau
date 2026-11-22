namespace Mau.Core.Engine;

/// <summary>A normalized ingredient key and the ingredient it identifies.</summary>
public sealed record LexiconEntry(string Key, int IngredientId);

/// <summary>One committed token from the typed text.</summary>
public sealed record ParsedToken(int Index, string Raw, string Normalized, int? IngredientId);

/// <summary>A hint for the word currently being typed.</summary>
public sealed record LexiconHint(string Text, int IngredientId);

/// <summary>The committed tokens, the pending prefix and its hints.</summary>
public sealed record ParseResult(
    IReadOnlyList<ParsedToken> Tokens, string PendingPrefix, IReadOnlyList<LexiconHint> Hints);
