using Mau.Core.Feedback;
using Mau.Core.Search;

namespace Mau.Core.Ranking;

/// <summary>
/// Scores each dish as popularity prior × preference boost × fit, then scales the scores to add up to 1.
/// Ties go to fewer missing ingredients, then shorter cooking time, then name.
/// </summary>
public sealed class RankingService : IRankingService
{
    private readonly IDishTotalsSource _totals;
    private readonly RankingOptions _options;

    public RankingService(IDishTotalsSource totals, RankingOptions options)
    {
        _totals = totals;
        _options = options;
    }

    public IReadOnlyList<DishMatch> Rank(IReadOnlyList<RankCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<DishMatch>();
        }

        var points = candidates
            .Select(candidate => Points(_totals.Get(candidate.Match.DishId)))
            .ToArray();
        var totalPoints = points.Sum();

        var raw = new double[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            var prior = (points[i] + 1) / (totalPoints + candidates.Count);
            var boost = Math.Pow(_options.PreferenceBoost, candidates[i].PreferenceMatches);
            raw[i] = prior * boost * Fit(candidates[i]);
        }

        var sum = raw.Sum();

        return candidates
            .Select((candidate, i) => candidate.Match with { Score = raw[i] / sum })
            .OrderByDescending(match => Math.Round(match.Score, 9))
            .ThenBy(match => match.MissingCount)
            .ThenBy(match => match.CookTimeMinutes)
            .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>2 per cook, +1 per thumbs up, -1 per thumbs down (by default), never below 0.</summary>
    public double Points(DishTotals totals) => Math.Max(
        0,
        (_options.CookedPoints * totals.CookedCount) +
        (_options.ThumbsUpPoints * totals.UpCount) -
        (_options.ThumbsDownPoints * totals.DownCount));

    private double Fit(RankCandidate candidate)
    {
        if (candidate.RequiredCount == 0)
        {
            return 1;
        }

        var have = candidate.RequiredCount - candidate.Match.MissingCount;
        return Math.Max(_options.FitFloor, (double)have / candidate.RequiredCount);
    }
}
