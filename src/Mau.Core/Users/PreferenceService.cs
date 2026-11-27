using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;

namespace Mau.Core.Users;

/// <summary>Preference rules on top of the store and the catalog.</summary>
public sealed class PreferenceService : IPreferenceService
{
    private readonly IPreferenceStore _store;
    private readonly INeverListStore _neverStore;
    private readonly CatalogStore _catalog;

    public PreferenceService(IPreferenceStore store, INeverListStore neverStore, CatalogStore catalog)
    {
        _store = store;
        _neverStore = neverStore;
        _catalog = catalog;
    }

    public async Task<IReadOnlySet<int>> GetNeverIdsAsync(
        string userId, CancellationToken ct = default)
    {
        var ids = await _neverStore.GetIngredientIdsAsync(userId, ct);
        return ids.Where(_catalog.Current.IngredientsById.ContainsKey).ToHashSet();
    }

    public async Task<int> SaveNeverIdsAsync(
        string userId, IEnumerable<int>? ingredientIds, CancellationToken ct = default)
    {
        var ids = (ingredientIds ?? Array.Empty<int>())
            .Where(_catalog.Current.IngredientsById.ContainsKey)
            .Distinct()
            .ToList();

        await _neverStore.ReplaceAsync(userId, ids, ct);
        return ids.Count;
    }

    public async Task<UserPreferences> GetAsync(string userId, CancellationToken ct = default)
    {
        var stored = await _store.GetAsync(userId, ct);

        // A protein removed from the catalog no longer counts.
        return stored.PreferredProteinId is int id &&
            !_catalog.Current.IngredientsById.ContainsKey(id)
            ? stored with { PreferredProteinId = null }
            : stored;
    }

    public async Task<IReadOnlyList<ValidationError>> SaveAsync(
        string userId, UserPreferences preferences, CancellationToken ct = default)
    {
        var errors = new List<ValidationError>();

        if (preferences.PreferredProteinId is int proteinId &&
            (!_catalog.Current.IngredientsById.TryGetValue(proteinId, out var protein) ||
             protein.Category != IngredientCategory.Protein))
        {
            errors.Add(new ValidationError("PreferredProteinId", "Choose a protein from the list."));
        }

        if (preferences.PreferredMethod is CookingMethod method && !Enum.IsDefined(method))
        {
            errors.Add(new ValidationError("PreferredMethod", "Choose a cooking method from the list."));
        }

        if (preferences.MaxCookTimeMinutes is int minutes &&
            (minutes < UserPreferences.MinCookTime || minutes > UserPreferences.MaxCookTime))
        {
            errors.Add(new ValidationError(
                "MaxCookTimeMinutes",
                $"Maximum cooking time must be between {UserPreferences.MinCookTime} and " +
                $"{UserPreferences.MaxCookTime} minutes."));
        }

        if (errors.Count == 0)
        {
            await _store.SaveAsync(userId, preferences, ct);
        }

        return errors;
    }
}
