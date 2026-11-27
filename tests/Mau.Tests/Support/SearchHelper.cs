using System.Net;
using System.Text.Json;

namespace Mau.Tests.Support;

public static class SearchHelper
{
    /// <summary>Runs a search through the API and returns the search token it issued.</summary>
    public static async Task<string> GetSearchTokenAsync(
        HttpClient client, string csrfToken, string text = "chicken,")
    {
        using var response = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text }, csrfToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Search failed with status {(int)response.StatusCode}.");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("searchToken").GetString()
            ?? throw new InvalidOperationException("The search returned no token.");
    }
}
