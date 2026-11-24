namespace Mau.Core.Admin;

/// <summary>A catalog write failed because of a database conflict.</summary>
public sealed class CatalogConflictException : Exception
{
    public CatalogConflictException(string message) : base(message)
    {
    }
}
