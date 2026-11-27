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

        using var response = await PostAsync(client, DishId("Chicken adobo"), "cooked", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoggedIn_Cooked_ReturnsCountsAndUpdatesCache()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var dishId = DishId("Pork adobo");
        var before = Cache().Get(dishId).CookedCount;

        using var response = await PostAsync(client, dishId, "cooked", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("cookedByYou").GetInt32());
        Assert.Equal(before + 1, json.RootElement.GetProperty("cookedCount").GetInt32());
        Assert.Equal(before + 1, Cache().Get(dishId).CookedCount);
    }

    [Fact]
    public async Task LoggedIn_ThumbsUpTwice_TogglesOff()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var dishId = DishId("Chicken tinola");

        using (var first = await PostAsync(client, dishId, "thumbsUp", token))
        {
            Assert.Equal("up", await ReadVoteAsync(first));
        }

        using var second = await PostAsync(client, dishId, "thumbsUp", token);
        Assert.Equal("none", await ReadVoteAsync(second));
    }

    [Fact]
    public async Task LoggedIn_UnknownDish_ReturnsNotFound()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostAsync(client, 999999, "cooked", token);

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

        using var response = await PostAsync(client, DishId("Chicken adobo"), type, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task LoggedIn_WithoutCsrfToken_IsRejected()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await PostAsync(client, DishId("Chicken adobo"), "cooked", "bad-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private int DishId(string name) =>
        _factory.Services.GetRequiredService<CatalogStore>().Current.DishesById.Values
            .Single(dish => dish.Name == name).Id;

    private DishStatsCache Cache() => _factory.Services.GetRequiredService<DishStatsCache>();

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, int dishId, string type, string token) =>
        AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/feedback", new { dishId, type }, token);

    private static async Task<string?> ReadVoteAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("vote").GetString();
    }
}
