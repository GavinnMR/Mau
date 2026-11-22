namespace Mau.Core.Catalog;

/// <summary>Loads the whole catalog into an immutable snapshot.</summary>
public interface ICatalogLoader
{
    Task<CatalogSnapshot> LoadAsync(CancellationToken ct = default);
}
