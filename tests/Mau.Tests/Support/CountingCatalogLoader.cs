using Mau.Core.Catalog;

namespace Mau.Tests.Support;

public sealed class CountingCatalogLoader : ICatalogLoader
{
    private readonly Func<CatalogSnapshot> _load;

    public CountingCatalogLoader(CatalogSnapshot snapshot) : this(() => snapshot)
    {
    }

    public CountingCatalogLoader(Func<CatalogSnapshot> load)
    {
        _load = load;
    }

    public int LoadCount { get; private set; }

    public Task<CatalogSnapshot> LoadAsync(CancellationToken ct = default)
    {
        LoadCount++;
        return Task.FromResult(_load());
    }
}
