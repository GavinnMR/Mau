using System.Net;
using System.Text.RegularExpressions;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class ProfilePantryTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public ProfilePantryTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_GetPantry_RedirectsToLogin()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Profile/Pantry");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/Account/Login", location);
        Assert.Contains("ReturnUrl=%2FProfile%2FPantry", location);
    }

    [Fact]
    public async Task LoggedIn_GetPantry_ShowsNonStapleCheckboxesOnly()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();

        using var response = await client.GetAsync("/Profile/Pantry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Your pantry", html);
        Assert.Equal(
            catalog.Ingredients.Count(ingredient => !ingredient.IsStaple),
            Regex.Matches(html, "type=\"checkbox\"").Count);

        foreach (var staple in catalog.Ingredients.Where(ingredient => ingredient.IsStaple))
        {
            Assert.DoesNotContain($"id=\"ing-{staple.Id}\"", html);
            Assert.DoesNotContain($"for=\"ing-{staple.Id}\"", html);
        }
    }

    [Fact]
    public async Task PostGarlicAndOnion_SavesBothAndShowsMessageOnce()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var onion = FindIngredient(catalog, "onion");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Pantry",
            Selected(garlic.Id, onion.Id),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/Pantry", response.Headers.Location?.ToString());

        using var first = await client.GetAsync("/Profile/Pantry");
        var html = await first.Content.ReadAsStringAsync();
        Assert.Equal(2, Regex.Matches(html, "checked").Count);
        Assert.True(IsChecked(html, garlic.Id));
        Assert.True(IsChecked(html, onion.Id));
        Assert.Contains("Pantry saved: 2 ingredients.", html);

        using var second = await client.GetAsync("/Profile/Pantry");
        Assert.DoesNotContain("Pantry saved:", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostGarlicUnknownAndStaple_SavesOnlyGarlic()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var salt = FindIngredient(catalog, "salt");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Pantry",
            Selected(garlic.Id, 99999, salt.Id),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var page = await client.GetAsync("/Profile/Pantry");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Pantry saved: 1 ingredient.", html);
        Assert.True(IsChecked(html, garlic.Id));
        Assert.Single(Regex.Matches(html, "checked"));
    }

    [Fact]
    public async Task PostNoFields_ClearsPantryAndSaysZeroIngredients()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using var first = await AuthHelper.PostFormAsync(
            client, "/Profile/Pantry", Selected(garlic.Id), token);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        token = await AuthHelper.GetCsrfTokenAsync(client);
        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Pantry",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var page = await client.GetAsync("/Profile/Pantry");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Pantry saved: 0 ingredients.", html);
        Assert.Empty(Regex.Matches(html, "checked"));
    }

    [Fact]
    public async Task PostWithoutToken_ReturnsBadRequest()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");

        using var response = await client.PostAsync(
            "/Profile/Pantry",
            new FormUrlEncodedContent(Selected(garlic.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TwoUsers_PantriesAreIsolated()
    {
        using var userA = AuthHelper.CreateClient(_factory);
        using var userB = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(userA);
        await AuthHelper.RegisterAsync(userB);
        var catalog = GetCatalog();
        var garlic = FindIngredient(catalog, "garlic");
        var token = await AuthHelper.GetCsrfTokenAsync(userA);

        using var save = await AuthHelper.PostFormAsync(
            userA, "/Profile/Pantry", Selected(garlic.Id), token);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        using var pageA = await userA.GetAsync("/Profile/Pantry");
        using var pageB = await userB.GetAsync("/Profile/Pantry");
        Assert.True(IsChecked(await pageA.Content.ReadAsStringAsync(), garlic.Id));
        Assert.Empty(Regex.Matches(await pageB.Content.ReadAsStringAsync(), "checked"));
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;

    private static CatalogIngredient FindIngredient(CatalogSnapshot catalog, string name) =>
        catalog.Ingredients.Single(ingredient => ingredient.CanonicalName == name);

    private static IEnumerable<KeyValuePair<string, string>> Selected(params int[] ids) =>
        ids.Select(id => new KeyValuePair<string, string>("SelectedIds", id.ToString()));

    private static bool IsChecked(string html, int id) =>
        Regex.IsMatch(html, $"id=\"ing-{id}\"[^>]*checked");
}
