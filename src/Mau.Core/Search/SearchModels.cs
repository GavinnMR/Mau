using Mau.Core.Entities;
using Mau.Core.Users;

namespace Mau.Core.Search;

/// <summary>The typed text, the ingredient ids the user left out, the pantry ids and preferences.</summary>
public sealed record SearchRequest(
    string? Text, IReadOnlyCollection<int>? ExcludedIds, IReadOnlyCollection<int>? PantryIds = null,
    UserPreferences? Preferences = null);

/// <summary>One committed token as shown to the user.</summary>
public sealed record SearchToken(int Index, string Raw, int? IngredientId, string? IngredientName, bool IsExcluded);

/// <summary>A dish that contains every included ingredient.</summary>
public sealed record DishMatch(
    int DishId, string Name, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> StillNeeded, double Score = 0, bool OverTimeLimit = false)
{
    public int MissingCount => StillNeeded.Count;
}

/// <summary>The tokens and matching dishes for one search.</summary>
/// <remarks>
/// Dishes longer than the user's maximum cooking time stay in the list, after all the others;
/// OverTimeLimitCount says how many there are.
/// </remarks>
public sealed record SearchResult(
    IReadOnlyList<SearchToken> Tokens, IReadOnlyList<DishMatch> Dishes, int OverTimeLimitCount = 0);

/// <summary>A hint for the word currently being typed.</summary>
public sealed record IngredientHint(string Text, int IngredientId, string IngredientName);
