using System.Collections.Immutable;

namespace Mau.Core.Feedback;

/// <summary>Keeps every dish's feedback totals in memory so searches never wait on the database.</summary>
public sealed class DishStatsCache : IDishTotalsSource
{
    private readonly IFeedbackStore _store;
    private ImmutableDictionary<int, DishTotals> _totals = ImmutableDictionary<int, DishTotals>.Empty;

    public DishStatsCache(IFeedbackStore store)
    {
        _store = store;
    }

    public IReadOnlyDictionary<int, DishTotals> Current => Volatile.Read(ref _totals);

    public DishTotals Get(int dishId) => Current.GetValueOrDefault(dishId, DishTotals.Empty);

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var totals = await _store.GetAllTotalsAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _totals, totals.ToImmutableDictionary());
    }

    /// <summary>Stores the totals the database returned after a feedback write.</summary>
    public void Set(int dishId, DishTotals totals) =>
        ImmutableInterlocked.AddOrUpdate(ref _totals, dishId, totals, (_, _) => totals);
}
