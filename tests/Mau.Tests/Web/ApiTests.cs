using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mau.Tests.Web;

public class ApiTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public ApiTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHints_Baw_ReturnsOnlyBawang()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/hints?prefix=baw");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var hints = json.RootElement.GetProperty("hints");
        Assert.Equal(1, hints.GetArrayLength());
        Assert.Equal("bawang", hints[0].GetProperty("text").GetString());
        Assert.Equal("garlic", hints[0].GetProperty("ingredient").GetString());
    }

    [Fact]
    public async Task GetHints_Ba_ReturnsFiveHintsInOrder()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/hints?prefix=ba");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var hints = json.RootElement.GetProperty("hints");
        var actual = hints.EnumerateArray()
            .Select(hint => (
                hint.GetProperty("text").GetString(),
                hint.GetProperty("ingredient").GetString()))
            .ToArray();

        (string? Text, string? Ingredient)[] expected =
        {
            ("baboy", "pork"),
            ("bagoong", "shrimp paste"),
            ("baka", "beef"),
            ("bawang", "garlic"),
            ("bay leaf", "bay leaf"),
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetHints_NoPrefix_ReturnsEmptyHints()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/hints");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("hints").GetArrayLength());
    }

    [Fact]
    public async Task GetHints_PrefixLongerThanFiftyCharacters_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var prefix = new string('a', 51);

        using var response = await client.GetAsync($"/api/hints?prefix={prefix}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetString())
            .ToArray();
        Assert.Contains("Prefix must be at most 50 characters.", errors);
    }

    [Fact]
    public async Task GetDish_ExistingId_ReturnsDishDetails()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/dishes/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dish = json.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(dish.GetProperty("name").GetString()));
        Assert.True(dish.GetProperty("ingredients").GetArrayLength() > 0);
        Assert.True(dish.GetProperty("steps").GetArrayLength() > 0);
        Assert.Equal(JsonValueKind.String, dish.GetProperty("cookingMethod").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(dish.GetProperty("cookingMethod").GetString()));
    }

    [Fact]
    public async Task GetDish_UnknownId_ReturnsNotFound()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/dishes/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetString())
            .ToArray();
        Assert.Contains("Dish not found.", errors);
    }

    [Fact]
    public async Task GetDish_NonNumericId_ReturnsNotFound()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/dishes/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSearch_ChickenAndGarlic_ReturnsTwoTokensAndGarlicFriedChickenFirst()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var response = await PostSearchAsync(
            client, new { text = "chicken, garlic," }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("tokens").GetArrayLength());
        Assert.Equal(
            "Garlic fried chicken",
            json.RootElement.GetProperty("dishes")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task PostSearch_KnownAndUnknownTokens_ReturnsIngredientDetails()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var response = await PostSearchAsync(
            client, new { text = "toyo, chiken," }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var tokens = json.RootElement.GetProperty("tokens");
        Assert.NotEqual(JsonValueKind.Null, tokens[0].GetProperty("ingredientId").ValueKind);
        Assert.Equal("soy sauce", tokens[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, tokens[1].GetProperty("ingredientId").ValueKind);
    }

    [Fact]
    public async Task PostSearch_ExcludingPork_RemovesPorkDishes()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var porkResponse = await PostSearchAsync(client, new { text = "pork," }, token);
        Assert.Equal(HttpStatusCode.OK, porkResponse.StatusCode);
        using var porkJson = JsonDocument.Parse(await porkResponse.Content.ReadAsStringAsync());
        var porkId = porkJson.RootElement
            .GetProperty("tokens")[0]
            .GetProperty("ingredientId")
            .GetInt32();

        using var response = await PostSearchAsync(
            client, new { text = "garlic,", excludedIds = new[] { porkId } }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dishes = json.RootElement.GetProperty("dishes").EnumerateArray();
        Assert.DoesNotContain(dishes, dish =>
            dish.GetProperty("name").GetString()?.Contains(
                "Pork", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task PostSearch_EmptyText_ReturnsNoDishes()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var response = await PostSearchAsync(client, new { text = "" }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("dishes").GetArrayLength());
    }

    [Fact]
    public async Task PostSearch_GuestWithUsePantry_IgnoresPantryAndMatchesNormalSearch()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var withPantry = await PostSearchAsync(
            client, new { text = "chicken,", usePantry = true }, token);
        using var withoutPantry = await PostSearchAsync(
            client, new { text = "chicken,", usePantry = false }, token);

        Assert.Equal(HttpStatusCode.OK, withPantry.StatusCode);
        using var withJson = JsonDocument.Parse(await withPantry.Content.ReadAsStringAsync());
        using var withoutJson = JsonDocument.Parse(await withoutPantry.Content.ReadAsStringAsync());

        Assert.False(withJson.RootElement.GetProperty("pantryApplied").GetBoolean());
        Assert.Equal(
            withoutJson.RootElement.GetProperty("dishes").GetRawText(),
            withJson.RootElement.GetProperty("dishes").GetRawText());
    }

    [Fact]
    public async Task PostSearch_TextLongerThanFiveHundredCharacters_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var token = await CsrfHelper.GetTokenAsync(client);

        using var response = await PostSearchAsync(
            client, new { text = new string('a', 501) }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostSearch_WithoutCsrfToken_ReturnsBadRequest()
    {
        using var client = CreateClient();

        using var response = await PostSearchAsync(
            client, new { text = "chicken," }, token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostSearchAsync(
        HttpClient client, object body, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/search")
        {
            Content = JsonContent.Create(body),
        };

        if (token is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
}
