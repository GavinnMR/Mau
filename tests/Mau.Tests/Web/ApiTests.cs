using System.Net;
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

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
}
