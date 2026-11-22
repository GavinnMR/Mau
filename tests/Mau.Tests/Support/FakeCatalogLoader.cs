using Mau.Core.Catalog;

namespace Mau.Tests.Support;

public sealed class FakeCatalogLoader : ICatalogLoader
{
    private readonly Func<CatalogSnapshot> _load;

    public FakeCatalogLoader(CatalogSnapshot snapshot) : this(() => snapshot)
    {
    }

    public FakeCatalogLoader(Func<CatalogSnapshot> load)
    {
        _load = load;
    }

    public Task<CatalogSnapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(_load());
}
