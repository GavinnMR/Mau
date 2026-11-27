using Mau.Core.Entities;
using Mau.Core.Feedback;
using Mau.Core.Ranking;
using Mau.Core.Search;

namespace Mau.Tests.Core;

public class RankingServiceTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 1, 0, 3)]
    [InlineData(0, 1, 3, 0)]
    [InlineData(2, 0, 1, 3)]
    public void Points_FollowTheFsdFormula(int cooked, int up, int down, double expected)
    {
        var ranking = Create(new Totals());

        Assert.Equal(expected, ranking.Points(new DishTotals(cooked, up, down)));
    }

    [Fact]
    public void NoFeedback_ScoresByFitAndAddsUpToOne()
    {
        var ranking = Create(new Totals());

        var ranked = ranking.Rank(new[]
        {
            Candidate(1, "Half", required: 4, missing: 2),
            Candidate(2, "Full", required: 2, missing: 0),
        });

        Assert.Equal(new[] { "Full", "Half" }, ranked.Select(match => match.Name));
        Assert.Equal(1.0, ranked.Sum(match => match.Score), 9);
        Assert.Equal(2.0 / 3, ranked[0].Score, 9);
        Assert.Equal(1.0 / 3, ranked[1].Score, 9);
    }

    [Fact]
    public void Feedback_CanLiftADishAboveABetterFit()
    {
        var totals = new Totals { [1] = new DishTotals(CookedCount: 5, UpCount: 0, DownCount: 0) };
        var ranking = Create(totals);

        var ranked = ranking.Rank(new[]
        {
            Candidate(1, "Popular", required: 4, missing: 2),
            Candidate(2, "Unrated", required: 2, missing: 0),
        });

        // Prior: Popular (10+1)/(10+2), Unrated 1/12. Raw: 11/12 x 0.5 vs 1/12 x 1.
        Assert.Equal("Popular", ranked[0].Name);
        Assert.Equal(5.5 / 6.5, ranked[0].Score, 9);
    }

    [Fact]
    public void ThumbsDownBelowZero_CountsAsZeroPoints()
    {
        var totals = new Totals { [1] = new DishTotals(0, 0, 4) };
        var ranking = Create(totals);

        var ranked = ranking.Rank(new[]
        {
            Candidate(1, "Disliked", required: 2, missing: 0),
            Candidate(2, "Unrated", required: 2, missing: 0),
        });

        Assert.Equal(0.5, ranked[0].Score, 9);
        Assert.Equal(0.5, ranked[1].Score, 9);
    }

    [Fact]
    public void NothingOwned_UsesFitFloor()
    {
        var ranking = Create(new Totals());

        var ranked = ranking.Rank(new[]
        {
            Candidate(1, "None", required: 4, missing: 4),
            Candidate(2, "All", required: 4, missing: 0),
        });

        Assert.Equal(0.05 / 1.05, ranked.Single(match => match.Name == "None").Score, 9);
    }

    [Fact]
    public void Ties_BreakByMissingThenTimeThenName()
    {
        var ranking = Create(new Totals());

        var ranked = ranking.Rank(new[]
        {
            Candidate(1, "B slow", required: 2, missing: 1, minutes: 60),
            Candidate(2, "More missing", required: 4, missing: 2, minutes: 10),
            Candidate(3, "A slow", required: 2, missing: 1, minutes: 60),
            Candidate(4, "Fast", required: 2, missing: 1, minutes: 20),
        });

        Assert.Equal(
            new[] { "Fast", "A slow", "B slow", "More missing" },
            ranked.Select(match => match.Name));
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        Assert.Empty(Create(new Totals()).Rank(Array.Empty<RankCandidate>()));
    }

    [Fact]
    public void Options_ChangeThePoints()
    {
        var ranking = new RankingService(
            new Totals(), new RankingOptions { CookedPoints = 3, ThumbsUpPoints = 2 });

        Assert.Equal(8, ranking.Points(new DishTotals(2, 1, 0)));
    }

    private static RankingService Create(Totals totals) => new(totals, new RankingOptions());

    private static RankCandidate Candidate(
        int id, string name, int required, int missing, int minutes = 30)
    {
        var stillNeeded = Enumerable.Range(0, missing).Select(i => $"item {i}").ToList();
        return new RankCandidate(
            new DishMatch(id, name, CookingMethod.Braised, minutes, stillNeeded),
            required);
    }

    private sealed class Totals : Dictionary<int, DishTotals>, IDishTotalsSource
    {
        public DishTotals Get(int dishId) =>
            TryGetValue(dishId, out var totals) ? totals : DishTotals.Empty;
    }
}
