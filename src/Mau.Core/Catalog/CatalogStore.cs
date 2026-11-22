namespace Mau.Core.Catalog;

/// <summary>Holds the current catalog snapshot and reloads it.</summary>
public sealed class CatalogStore
{
    private readonly ICatalogLoader _loader;
    private CatalogSnapshot? _current;

    public CatalogStore(ICatalogLoader loader)
    {
        _loader = loader;
    }

    public CatalogSnapshot Current =>
        Volatile.Read(ref _current) ?? throw new InvalidOperationException("Catalog not loaded.");

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var snapshot = await _loader.LoadAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _current, snapshot);
    }
}
