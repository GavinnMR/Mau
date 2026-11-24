namespace Mau.Core.Users;

/// <summary>One recorded search as returned from the log store.</summary>
public sealed record SearchLogEntry(
    int Id, DateTime SearchedAtUtc, int ResultCount, IReadOnlyList<int> IngredientIds);
