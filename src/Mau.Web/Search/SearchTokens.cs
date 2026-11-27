using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Mau.Web.Search;

/// <summary>
/// Issues a signed token with every search and reads it back. The token's key lets a user mark
/// a dish as cooked once per search: a dish opened from a search result carries the token.
/// </summary>
public sealed class SearchTokens
{
    private readonly IDataProtector _protector;

    public SearchTokens(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Mau.SearchToken.v1");
    }

    /// <summary>A new token holding a fresh random key.</summary>
    public string Issue() => _protector.Protect(Guid.NewGuid().ToString("N"));

    /// <summary>The key inside a token this app issued, or null for a missing or forged token.</summary>
    public string? ReadKey(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 500)
        {
            return null;
        }

        try
        {
            var key = _protector.Unprotect(token);
            return key.Length == 32 ? key : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
