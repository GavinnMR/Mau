using System.Net;
using System.Text.Json;
using Mau.Core.Catalog;
using Mau.Core.Feedback;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class FeedbackApiTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public FeedbackApiTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_Post_ReturnsUnauthorized()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var search = await SearchHelper.GetSearchTokenAsync(client, token);

        using var response = await PostAsync(client, DishId("Chicken adobo"), "cooked", search, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoggedIn_CookedFromSearch_ReturnsCountsAndUpdatesCache()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var search = await SearchHelper.GetSearchTokenAsync(client, token, "pork,");
        var dishId = DishId("Pork adobo");
        var before = Cache().Get(dishId).CookedCount;

        using var response = await PostAsync(client, dishId, "cooked", search, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("cookedByYou").GetInt32());
        Assert.Equal(before + 1, json.RootElement.GetProperty("cookedCount").GetInt32());
        Assert.Equal(before + 1, Cache().Get(dishId).CookedCount);
    }

    [Fact]
    public async Task LoggedIn_CookedTwiceSameSearch_SecondIsConflictAndNotCounted()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var search = await SearchHelper.GetSearchTokenAsync(client, token);
        var dishId = DishId("Chicken afritada");

        using (var first = await PostAsync(client, dishId, "cooked", search, token))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var afterFirst = Cache().Get(dishId).CookedCount;
        using var second = await PostAsync(client, dishId, "cooked", search, token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(afterFirst, Cache().Get(dishId).CookedCount);
    }

    [Fact]
    public async Task LoggedIn_CookedAfterNewSearch_CountsAgain()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var dishId = DishId("Chicken afritada");

        foreach (var expected in new[] { 1, 2 })
        {
            var search = await SearchHelper.GetSearchTokenAsync(client, token);
            using var response = await PostAsync(client, dishId, "cooked", search, token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected, json.RootElement.GetProperty("cookedByYou").GetInt32());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("forged-token")]
    public async Task LoggedIn_CookedWithoutValidSearch_ReturnsBadRequest(string? search)
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostAsync(client, DishId("Chicken adobo"), "cooked", search, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("search results", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoggedIn_ThumbsUpTwice_TogglesOff_WithoutSearch()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var dishId = DishId("Chicken tinola");

        using (var first = await PostAsync(client, dishId, "thumbsUp", null, token))
        {
            Assert.Equal("up", await ReadVoteAsync(first));
        }

        using var second = await PostAsync(client, dishId, "thumbsUp", null, token);
        Assert.Equal("none", await ReadVoteAsync(second));
    }

    [Fact]
    public async Task LoggedIn_UnknownDish_ReturnsNotFound()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var search = await SearchHelper.GetSearchTokenAsync(client, token);

        using var response = await PostAsync(client, 999999, "cooked", search, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("loved")]
    [InlineData("")]
    public async Task LoggedIn_BadType_ReturnsBadRequest(string type)
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostAsync(client, DishId("Chicken adobo"), type, null, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task LoggedIn_WithoutCsrfToken_IsRejected()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await PostAsync(client, DishId("Chicken adobo"), "thumbsUp", null, "bad-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithResults_IssuesToken_WithoutResults_DoesNot()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        Assert.False(string.IsNullOrEmpty(await SearchHelper.GetSearchTokenAsync(client, token)));

        using var response = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text = "chicken, pork," }, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("searchToken").ValueKind);
    }

    private int DishId(string name) =>
        _factory.Services.GetRequiredService<CatalogStore>().Current.DishesById.Values
            .Single(dish => dish.Name == name).Id;

    private DishStatsCache Cache() => _factory.Services.GetRequiredService<DishStatsCache>();

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, int dishId, string type, string? search, string token) =>
        AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/feedback", new { dishId, type, search }, token);

    private static async Task<string?> ReadVoteAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("vote").GetString();
    }
}

public class FeedbackPageTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public FeedbackPageTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_SeesTotalsAndLoginLink_NoButtons()
    {
        using var client = AuthHelper.CreateClient(_factory);

        var html = await GetDetailsAsync(client, "Chicken adobo");

        Assert.Contains("id=\"feedback-totals\"", html);
        Assert.Contains("Log in to rate this dish", html);
        Assert.DoesNotContain("data-feedback=\"cooked\"", html);
        Assert.DoesNotContain("feedback.js", html);
    }

    [Fact]
    public async Task LoggedIn_OpenedDirectly_CookedIsDisabledWithHint()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        var html = await GetDetailsAsync(client, "Chicken adobo");

        Assert.Matches("data-feedback=\"cooked\" disabled\\s+aria-describedby=\"cooked-hint\"", html);
        Assert.Contains("Open this dish from your search results", html);
    }

    [Fact]
    public async Task LoggedIn_OpenedFromSearch_CookedIsAvailable_ThenDone()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var search = await SearchHelper.GetSearchTokenAsync(client, token);
        var dishId = DishId("Garlic fried chicken");

        var before = await GetDetailsAsync(client, "Garlic fried chicken", search);
        Assert.Contains("data-feedback=\"cooked\">I cooked this</button>", before);
        Assert.DoesNotContain("cooked-hint", before);

        foreach (var type in new[] { "cooked", "thumbsDown" })
        {
            using var response = await AuthHelper.SendJsonAsync(
                client, HttpMethod.Post, "/api/feedback", new { dishId, type, search }, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var after = await GetDetailsAsync(client, "Garlic fried chicken", search);

        Assert.Contains("data-feedback=\"cooked\" disabled>Marked as cooked", after);
        Assert.Matches(
            "data-feedback=\"thumbsDown\"\\s+aria-label=\"Thumbs down\" aria-pressed=\"true\"", after);
        Assert.Contains("You cooked this 1 time.", after);
        Assert.Contains("feedback.js", after);
    }

    private int DishId(string name) =>
        _factory.Services.GetRequiredService<CatalogStore>().Current.DishesById.Values
            .Single(dish => dish.Name == name).Id;

    private async Task<string> GetDetailsAsync(HttpClient client, string name, string? search = null)
    {
        var url = $"/Dish/Details/{DishId(name)}" +
            (search is null ? "" : "?search=" + Uri.EscapeDataString(search));
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
