using Mau.Core.Feedback;

namespace Mau.Tests.Support;

public sealed class FakeFeedbackStore : IFeedbackStore
{
    private readonly Dictionary<(string User, int Dish), int> _cooked = new();
    private readonly Dictionary<(string User, int Dish), Vote> _votes = new();

    public Task<DishTotals> AddCookedAsync(string userId, int dishId, CancellationToken ct = default)
    {
        _cooked[(userId, dishId)] = _cooked.GetValueOrDefault((userId, dishId)) + 1;
        return Task.FromResult(Totals(dishId));
    }

    public Task<DishTotals> SetVoteAsync(
        string userId, int dishId, Vote vote, CancellationToken ct = default)
    {
        if (vote == Vote.None)
        {
            _votes.Remove((userId, dishId));
        }
        else
        {
            _votes[(userId, dishId)] = vote;
        }

        return Task.FromResult(Totals(dishId));
    }

    public Task<UserDishFeedback> GetUserFeedbackAsync(
        string userId, int dishId, CancellationToken ct = default) =>
        Task.FromResult(new UserDishFeedback(
            _cooked.GetValueOrDefault((userId, dishId)),
            _votes.GetValueOrDefault((userId, dishId), Vote.None)));

    public Task<IReadOnlyDictionary<int, DishTotals>> GetAllTotalsAsync(CancellationToken ct = default)
    {
        var dishIds = _cooked.Keys.Select(key => key.Dish)
            .Concat(_votes.Keys.Select(key => key.Dish))
            .Distinct();
        IReadOnlyDictionary<int, DishTotals> totals = dishIds.ToDictionary(id => id, Totals);
        return Task.FromResult(totals);
    }

    private DishTotals Totals(int dishId) => new(
        _cooked.Where(pair => pair.Key.Dish == dishId).Sum(pair => pair.Value),
        _votes.Count(pair => pair.Key.Dish == dishId && pair.Value == Vote.Up),
        _votes.Count(pair => pair.Key.Dish == dishId && pair.Value == Vote.Down));
}
