using Mau.Core.Entities;

namespace Mau.Core.Search;

/// <summary>The typed text, the ingredient ids the user left out and the pantry ids.</summary>
public sealed record SearchRequest(
    string? Text, IReadOnlyCollection<int>? ExcludedIds, IReadOnlyCollection<int>? PantryIds = null);

/// <summary>One committed token as shown to the user.</summary>
public sealed record SearchToken(int Index, string Raw, int? IngredientId, string? IngredientName, bool IsExcluded);

/// <summary>A dish that contains every included ingredient.</summary>
public sealed record DishMatch(
    int DishId, string Name, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> StillNeeded)
{
    public int MissingCount => StillNeeded.Count;
}

/// <summary>The tokens and matching dishes for one search.</summary>
public sealed record SearchResult(IReadOnlyList<SearchToken> Tokens, IReadOnlyList<DishMatch> Dishes);

/// <summary>A hint for the word currently being typed.</summary>
public sealed record IngredientHint(string Text, int IngredientId, string IngredientName);
