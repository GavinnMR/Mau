namespace Mau.Core.Engine;

/// <summary>Fuzzy matching limits (FR-10), read from the "Fuzzy" config section.</summary>
public sealed class FuzzyOptions
{
    public const string SectionName = "Fuzzy";

    /// <summary>Largest edit distance for short words.</summary>
    public int MaxDistance { get; set; } = 1;

    /// <summary>Largest edit distance for words of <see cref="LongWordLength"/> letters or more.</summary>
    public int MaxDistanceLongWords { get; set; } = 2;

    public int LongWordLength { get; set; } = 8;

    public int MaxSuggestions { get; set; } = 3;
}
