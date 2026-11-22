using System.Text.RegularExpressions;

namespace Mau.Tests.Support;

public static class CsrfHelper
{
    private static readonly Regex TokenPattern = new(
        "name=\"csrf-token\"\\s+content=\"([^\"]+)\"",
        RegexOptions.Compiled);

    public static async Task<string> GetTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = TokenPattern.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("CSRF token not found on the home page.");
        }

        return match.Groups[1].Value;
    }
}
