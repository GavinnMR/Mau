namespace Mau.Core.Ranking;

/// <summary>Ranking constants (FSD "Ranking and Learning"), read from the "Ranking" config section.</summary>
public sealed class RankingOptions
{
    public const string SectionName = "Ranking";

    /// <summary>Feedback points for each time a dish was cooked.</summary>
    public double CookedPoints { get; set; } = 2;

    /// <summary>Feedback points added for each thumbs up.</summary>
    public double ThumbsUpPoints { get; set; } = 1;

    /// <summary>Feedback points taken away for each thumbs down.</summary>
    public double ThumbsDownPoints { get; set; } = 1;

    /// <summary>Multiplier for each preference a dish meets (protein, cooking method).</summary>
    public double PreferenceBoost { get; set; } = 2;

    /// <summary>The lowest fit a matching dish can have, so its score is never zero.</summary>
    public double FitFloor { get; set; } = 0.05;
}
