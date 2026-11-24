using Mau.Core.Entities;

namespace Mau.Core.Admin;

/// <summary>An ingredient submitted from an admin form.</summary>
public sealed record IngredientInput(
    string? Name, IngredientCategory Category, bool IsStaple,
    IReadOnlyList<SynonymInput> Synonyms);
