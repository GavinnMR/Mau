using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Data.Identity;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class HistoryTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public HistoryTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_PostSearchLog_LogsKnownIngredientsOnly()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using (var known = await PostLogAsync(client, new { text = "chicken, garlic," }, token))
        {
            Assert.Equal(HttpStatusCode.OK, known.StatusCode);
            Assert.True(await ReadLoggedAsync(known));
        }

        using (var unknown = await PostLogAsync(client, new { text = "dragonfruit," }, token))
        {
            Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
            Assert.False(await ReadLoggedAsync(unknown));
        }

        using (var empty = await PostLogAsync(client, new { text = "" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
            Assert.False(await ReadLoggedAsync(empty));
        }
    }

    [Fact]
    public async Task PostSearchLog_TextLongerThanFiveHundredCharacters_ReturnsBadRequest()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostLogAsync(
            client, new { text = new string('a', 501) }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostSearchLog_WithoutToken_ReturnsBadRequest()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await PostLogAsync(
            client, new { text = "chicken," }, token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoggedIn_History_ListsNewestFirstWithComputedCounts()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using (var first = await PostLogAsync(
            client, new { text = "chicken, garlic," }, token))
        {
            Assert.True(await ReadLoggedAsync(first));
        }

        using (var second = await PostLogAsync(client, new { text = "toyo," }, token))
        {
            Assert.True(await ReadLoggedAsync(second));
        }

        using var page = await client.GetAsync("/Profile/History");
        var html = await page.Content.ReadAsStringAsync();

        var soyIndex = html.IndexOf(">soy sauce<", StringComparison.Ordinal);
        var chickenIndex = html.IndexOf(">chicken<", StringComparison.Ordinal);
        var garlicIndex = html.IndexOf(">garlic<", StringComparison.Ordinal);

        Assert.True(soyIndex >= 0);
        Assert.True(chickenIndex >= 0);
        Assert.True(garlicIndex >= 0);
        Assert.True(soyIndex < chickenIndex);
        Assert.True(chickenIndex < garlicIndex);

        Assert.Contains("Search again", html);
        Assert.Contains("q=chicken", html);

        var search = _factory.Services.GetRequiredService<ISearchService>();
        var chickenCount = search
            .Search(new SearchRequest("chicken, garlic,", null)).Dishes.Count;
        var soyCount = search.Search(new SearchRequest("toyo,", null)).Dishes.Count;

        Assert.Contains(CountText(chickenCount), html);
        Assert.Contains(CountText(soyCount), html);
    }

    [Fact]
    public async Task GuestLog_DoesNotAppearInUserHistory()
    {
        using var guest = AuthHelper.CreateClient(_factory);
        var guestToken = await AuthHelper.GetCsrfTokenAsync(guest);
        using (var guestLog = await PostLogAsync(
            guest, new { text = "chicken, garlic," }, guestToken))
        {
            Assert.True(await ReadLoggedAsync(guestLog));
        }

        using var user = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(user);
        var userToken = await AuthHelper.GetCsrfTokenAsync(user);
        using (var userLog = await PostLogAsync(user, new { text = "toyo," }, userToken))
        {
            Assert.True(await ReadLoggedAsync(userLog));
        }

        using var page = await user.GetAsync("/Profile/History");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Contains(">soy sauce<", html);
        Assert.DoesNotContain(">chicken<", html);
        Assert.DoesNotContain(">garlic<", html);
    }

    [Fact]
    public async Task LoggedIn_DeleteHistory_RemovesRowAndShowsMessageOnce()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var log = await PostLogAsync(client, new { text = "chicken, garlic," }, token))
        {
            Assert.True(await ReadLoggedAsync(log));
        }

        using var page = await client.GetAsync("/Profile/History");
        var html = await page.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "/Profile/DeleteHistory/(\\d+)");
        Assert.True(match.Success);
        var logId = match.Groups[1].Value;

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Profile/DeleteHistory/{logId}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/History", response.Headers.Location?.ToString());

        using var after = await client.GetAsync("/Profile/History");
        var afterHtml = await after.Content.ReadAsStringAsync();
        Assert.DoesNotContain($"/Profile/DeleteHistory/{logId}", afterHtml);
        Assert.DoesNotContain(">chicken<", afterHtml);
        Assert.Contains("Search removed.", afterHtml);

        using var second = await client.GetAsync("/Profile/History");
        Assert.DoesNotContain(
            "Search removed.", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UserB_DeletingLogOfUserA_DoesNotRemoveIt()
    {
        using var userA = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(userA);
        var tokenA = await AuthHelper.GetCsrfTokenAsync(userA);
        using (var log = await PostLogAsync(
            userA, new { text = "chicken, garlic," }, tokenA))
        {
            Assert.True(await ReadLoggedAsync(log));
        }

        using var page = await userA.GetAsync("/Profile/History");
        var html = await page.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "/Profile/DeleteHistory/(\\d+)");
        Assert.True(match.Success);
        var logId = match.Groups[1].Value;

        using var userB = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(userB);
        var tokenB = await AuthHelper.GetCsrfTokenAsync(userB);

        using (var response = await AuthHelper.PostFormAsync(
            userB,
            $"/Profile/DeleteHistory/{logId}",
            Array.Empty<KeyValuePair<string, string>>(),
            tokenB))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var after = await userA.GetAsync("/Profile/History");
        var afterHtml = await after.Content.ReadAsStringAsync();
        Assert.Contains($"/Profile/DeleteHistory/{logId}", afterHtml);
        Assert.Contains(">chicken<", afterHtml);
    }

    [Fact]
    public async Task LoggedIn_ClearHistory_RemovesAllRows()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var log = await PostLogAsync(client, new { text = "chicken," }, token))
        {
            Assert.True(await ReadLoggedAsync(log));
        }

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Profile/ClearHistory",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/History", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Profile/History");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("History cleared.", html);
        Assert.Contains("No searches yet.", html);
    }

    [Fact]
    public async Task Guest_GetHistory_RedirectsToLogin()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Profile/History");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoggedIn_101stLog_TrimsOldest()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);

        var history = _factory.Services.GetRequiredService<ISearchHistoryService>();
        var store = _factory.Services.GetRequiredService<ISearchLogStore>();

        for (var i = 0; i < 101; i++)
        {
            Assert.True(await history.RecordAsync(user!.Id, "chicken,", null));
        }

        var entries = await store.GetRecentAsync(user.Id, 200);
        Assert.Equal(100, entries.Count);
    }

    private static async Task<HttpResponseMessage> PostLogAsync(
        HttpClient client, object body, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/search-log")
        {
            Content = JsonContent.Create(body),
        };

        if (token is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<bool> ReadLoggedAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("logged").GetBoolean();
    }

    private static string CountText(int count) =>
        count == 1 ? "1 dish found" : $"{count} dishes found";
}
