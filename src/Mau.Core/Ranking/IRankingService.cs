using Mau.Core.Search;

namespace Mau.Core.Ranking;

/// <summary>A matching dish to rank, with how many of its ingredients are required.</summary>
/// <remarks>PreferenceMatches: 0, 1 or 2 (preferred protein, preferred cooking method).</remarks>
public sealed record RankCandidate(DishMatch Match, int RequiredCount, int PreferenceMatches = 0);

/// <summary>Scores matching dishes and puts them in order (FR-07).</summary>
public interface IRankingService
{
    /// <summary>Returns the dishes with their scores, highest first; the scores add up to 1.</summary>
    IReadOnlyList<DishMatch> Rank(IReadOnlyList<RankCandidate> candidates);
}
