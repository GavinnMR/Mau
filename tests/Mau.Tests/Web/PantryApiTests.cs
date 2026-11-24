using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class PantryApiTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public PantryApiTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoggedInSearch_WithPantry_AppliesPantry()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var soySauce = FindIngredient(catalog, "soy sauce");
        var vinegar = FindIngredient(catalog, "vinegar");
        await SavePantryAsync(client, soySauce.Id, vinegar.Id);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var withPantry = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search",
            new { text = "chicken,", usePantry = true }, token);

        Assert.Equal(HttpStatusCode.OK, withPantry.StatusCode);
        using var withJson = JsonDocument.Parse(await withPantry.Content.ReadAsStringAsync());
        Assert.True(withJson.RootElement.GetProperty("pantryApplied").GetBoolean());
        Assert.Equal(
            new[] { "garlic" },
            StillNeeded(FindDish(withJson.RootElement, "Chicken adobo")));

        using var withoutPantry = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search",
            new { text = "chicken,", usePantry = false }, token);

        Assert.Equal(HttpStatusCode.OK, withoutPantry.StatusCode);
        using var withoutJson =
            JsonDocument.Parse(await withoutPantry.Content.ReadAsStringAsync());
        Assert.False(withoutJson.RootElement.GetProperty("pantryApplied").GetBoolean());
        Assert.Equal(
            new[] { "soy sauce", "vinegar", "garlic" },
            StillNeeded(FindDish(withoutJson.RootElement, "Chicken adobo")));
    }

    [Fact]
    public async Task LoggedInSearch_WithEmptyPantry_DoesNotApplyPantry()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search",
            new { text = "chicken,", usePantry = true }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("pantryApplied").GetBoolean());
    }

    [Fact]
    public async Task Guest_GetPantry_ReturnsUnauthorizedJson()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetString())
            .ToArray();
        Assert.Contains("Please log in.", errors);
    }

    [Fact]
    public async Task LoggedIn_GetPantry_ReturnsSavedIngredientsSortedByName()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var onion = FindIngredient(catalog, "onion");
        await SavePantryAsync(client, garlic.Id, onion.Id);

        using var response = await client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ingredients = json.RootElement.GetProperty("ingredients")
            .EnumerateArray()
            .Select(item => (
                Id: item.GetProperty("id").GetInt32(),
                Name: item.GetProperty("name").GetString()))
            .ToArray();

        Assert.Equal(2, ingredients.Length);
        Assert.Equal(garlic.Id, ingredients[0].Id);
        Assert.Equal("garlic", ingredients[0].Name);
        Assert.Equal(onion.Id, ingredients[1].Id);
        Assert.Equal("onion", ingredients[1].Name);
    }

    [Fact]
    public async Task GuestHome_HasNoPantryOption()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-authenticated=\"false\"", html);
        Assert.DoesNotContain("id=\"use-pantry\"", html);
    }

    [Fact]
    public async Task LoggedInHome_WithEmptyPantry_ShowsDisabledCheckboxAndSetupLink()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"use-pantry\"", html);
        Assert.True(PantryCheckboxDisabled(html));
        Assert.False(PantryCheckboxChecked(html));
        Assert.Contains("Set up your pantry", html);
    }

    [Fact]
    public async Task LoggedInHome_WithPantryOfTwo_ShowsCountAndCheckedCheckbox()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var onion = FindIngredient(catalog, "onion");
        await SavePantryAsync(client, garlic.Id, onion.Id);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("(2)", html);
        Assert.True(PantryCheckboxChecked(html));
        Assert.False(PantryCheckboxDisabled(html));
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;

    private static CatalogIngredient FindIngredient(CatalogSnapshot catalog, string name) =>
        catalog.Ingredients.Single(ingredient => ingredient.CanonicalName == name);

    private static JsonElement FindDish(JsonElement root, string name) =>
        root.GetProperty("dishes")
            .EnumerateArray()
            .Single(dish => dish.GetProperty("name").GetString() == name);

    private static string[] StillNeeded(JsonElement dish) =>
        dish.GetProperty("stillNeeded")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .ToArray();

    private static async Task SavePantryAsync(HttpClient client, params int[] ids)
    {
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var fields = ids.Select(
            id => new KeyValuePair<string, string>("SelectedIds", id.ToString()));
        using var response = await AuthHelper.PostFormAsync(
            client, "/Profile/Pantry", fields, token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static bool PantryCheckboxChecked(string html) =>
        Regex.IsMatch(html, "id=\"use-pantry\"[^>]*\\bchecked\\b");

    private static bool PantryCheckboxDisabled(string html) =>
        Regex.IsMatch(html, "id=\"use-pantry\"[^>]*\\bdisabled\\b");
}
