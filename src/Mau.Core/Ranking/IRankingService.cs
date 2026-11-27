using Mau.Core.Search;

namespace Mau.Core.Ranking;

/// <summary>A matching dish to rank, with how many of its ingredients are required.</summary>
public sealed record RankCandidate(DishMatch Match, int RequiredCount);

/// <summary>Scores matching dishes and puts them in order (FR-07).</summary>
public interface IRankingService
{
    /// <summary>Returns the dishes with their scores, highest first; the scores add up to 1.</summary>
    IReadOnlyList<DishMatch> Rank(IReadOnlyList<RankCandidate> candidates);
}
