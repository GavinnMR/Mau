using System.Net;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class PreferencesPageTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public PreferencesPageTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_GetPreferences_RedirectsToLogin()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Profile/Preferences");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("ReturnUrl=%2FProfile%2FPreferences", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoggedIn_Save_RedirectsAndFormShowsSavedValues()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var porkId = ProteinId("pork");

        using var response = await PostAsync(client, token, porkId.ToString(), "Soup", "45");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var page = await client.GetAsync("/Profile/Preferences");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Preferences saved.", html);
        Assert.Contains($"<option selected=\"selected\" value=\"{porkId}\">pork</option>", html);
        Assert.Contains("<option selected=\"selected\" value=\"Soup\">Soup</option>", html);
        Assert.Contains("value=\"45\"", html);
    }

    [Fact]
    public async Task LoggedIn_SaveOutOfRangeTime_ShowsErrorAndKeepsInput()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostAsync(client, token, "", "Fried", "2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Maximum cooking time must be between 5 and 600 minutes.", html);
        Assert.Contains("<option selected=\"selected\" value=\"Fried\">Fried</option>", html);
    }

    [Fact]
    public async Task LoggedIn_SaveNonProtein_ShowsError()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PostAsync(client, token, ProteinId("garlic").ToString(), "", "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose a protein from the list.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoggedIn_NavbarLinksToPreferences()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/");

        Assert.Contains("href=\"/Profile/Preferences\"", await response.Content.ReadAsStringAsync());
    }

    private int ProteinId(string name) =>
        _factory.Services.GetRequiredService<CatalogStore>().Current.Ingredients
            .Single(ingredient => ingredient.CanonicalName == name).Id;

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string token, string protein, string method, string minutes) =>
        AuthHelper.PostFormAsync(
            client,
            "/Profile/Preferences",
            new[]
            {
                new KeyValuePair<string, string>("PreferredProteinId", protein),
                new KeyValuePair<string, string>("PreferredMethod", method),
                new KeyValuePair<string, string>("MaxCookTimeMinutes", minutes),
            },
            token);
}
