using Mau.Core.Entities;
using Mau.Core.Users;

namespace Mau.Core.Search;

/// <summary>
/// The typed text, the ingredient ids the user left out, the pantry ids, the preferences and the
/// ingredients the user never wants.
/// </summary>
public sealed record SearchRequest(
    string? Text, IReadOnlyCollection<int>? ExcludedIds, IReadOnlyCollection<int>? PantryIds = null,
    UserPreferences? Preferences = null, IReadOnlyCollection<int>? NeverIds = null);

/// <summary>One committed token as shown to the user.</summary>
public sealed record SearchToken(int Index, string Raw, int? IngredientId, string? IngredientName, bool IsExcluded)
{
    /// <summary>For an unknown word, the closest known names ("Did you mean...?"), closest first.</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}

/// <summary>A dish that contains every included ingredient.</summary>
public sealed record DishMatch(
    int DishId, string Name, CookingMethod CookingMethod, int CookTimeMinutes,
    IReadOnlyList<string> StillNeeded, double Score = 0, bool OverTimeLimit = false)
{
    public int MissingCount => StillNeeded.Count;

    /// <summary>The preferred protein's name when the dish's main protein matches it.</summary>
    public string? MatchedProtein { get; init; }

    /// <summary>True when the dish uses the user's preferred cooking method.</summary>
    public bool MatchedMethod { get; init; }
}

/// <summary>The tokens and matching dishes for one search.</summary>
/// <remarks>
/// Dishes longer than the user's maximum cooking time stay in the list, after all the others;
/// OverTimeLimitCount says how many there are. Dishes that need an ingredient on the user's
/// never list are left out: NeverListHiddenCount says how many, NeverListBlocking which of
/// those ingredients caused it, and NeverListOverridden which of them the user typed anyway.
/// </remarks>
public sealed record SearchResult(
    IReadOnlyList<SearchToken> Tokens, IReadOnlyList<DishMatch> Dishes, int OverTimeLimitCount = 0)
{
    public int NeverListHiddenCount { get; init; }

    public IReadOnlyList<string> NeverListBlocking { get; init; } = [];

    public IReadOnlyList<string> NeverListOverridden { get; init; } = [];
}

/// <summary>A hint for the word currently being typed.</summary>
public sealed record IngredientHint(string Text, int IngredientId, string IngredientName);
