namespace Mau.Core.Catalog;

/// <summary>Thrown when catalog data is inconsistent.</summary>
public sealed class CatalogException : Exception
{
    public CatalogException(string message) : base(message) { }
}
