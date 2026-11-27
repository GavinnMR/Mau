using System.Net;
using System.Text.Json;
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

public class PreferencesSearchTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public PreferencesSearchTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoggedInWithPreferences_SearchAppliesThem()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var save = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Preferences",
            new[]
            {
                new KeyValuePair<string, string>("PreferredProteinId", ""),
                new KeyValuePair<string, string>("PreferredMethod", "Soup"),
                new KeyValuePair<string, string>("MaxCookTimeMinutes", "45"),
            },
            token))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        using var guestClient = AuthHelper.CreateClient(_factory);
        var guestToken = await AuthHelper.GetCsrfTokenAsync(guestClient);
        using var guest = await SearchAsync(guestClient, guestToken);
        using var json = await SearchAsync(client, token);

        var root = json.RootElement;
        Assert.True(root.GetProperty("preferencesApplied").GetBoolean());
        Assert.Equal(45, root.GetProperty("maxCookTimeMinutes").GetInt32());
        Assert.Equal(1, root.GetProperty("overTimeLimitCount").GetInt32());
        var dishes = root.GetProperty("dishes").EnumerateArray().ToList();
        var last = dishes[^1];
        Assert.Equal("Chicken afritada", last.GetProperty("name").GetString());
        Assert.True(last.GetProperty("overTimeLimit").GetBoolean());
        Assert.All(dishes.SkipLast(1), dish => Assert.False(dish.GetProperty("overTimeLimit").GetBoolean()));

        var tinola = dishes.Single(dish => dish.GetProperty("name").GetString() == "Chicken tinola");
        Assert.Equal(
            new[] { "Soup, your method" },
            tinola.GetProperty("preferenceBadges").EnumerateArray().Select(item => item.GetString()));
        var fried = dishes.Single(dish => dish.GetProperty("name").GetString() == "Garlic fried chicken");
        Assert.Empty(fried.GetProperty("preferenceBadges").EnumerateArray());

        // The Soup preference doubles tinola's score relative to a non-soup dish.
        var guestRatio = Score(guest, "Chicken tinola") / Score(guest, "Garlic fried chicken");
        var userRatio = Score(json, "Chicken tinola") / Score(json, "Garlic fried chicken");
        Assert.Equal(2 * guestRatio, userRatio, 2);
    }

    private static double Score(JsonDocument json, string name) =>
        json.RootElement.GetProperty("dishes").EnumerateArray()
            .Single(dish => dish.GetProperty("name").GetString() == name)
            .GetProperty("score").GetDouble();

    [Fact]
    public async Task LoggedInWithNeverList_SearchHidesDishesAndPageShowsTicks()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var vinegarId = _factory.Services.GetRequiredService<Mau.Core.Catalog.CatalogStore>()
            .Current.Ingredients.Single(ingredient => ingredient.CanonicalName == "vinegar").Id;
        using (var save = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Preferences",
            new[]
            {
                new KeyValuePair<string, string>("PreferredProteinId", ""),
                new KeyValuePair<string, string>("PreferredMethod", ""),
                new KeyValuePair<string, string>("MaxCookTimeMinutes", ""),
                new KeyValuePair<string, string>("NeverIds", vinegarId.ToString()),
            },
            token))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        using var json = await SearchAsync(client, token);

        var root = json.RootElement;
        Assert.DoesNotContain(
            root.GetProperty("dishes").EnumerateArray(),
            dish => dish.GetProperty("name").GetString() == "Chicken adobo");
        Assert.Equal(1, root.GetProperty("neverListHiddenCount").GetInt32());
        Assert.Equal("vinegar", root.GetProperty("neverListBlocking")[0].GetString());

        using var page = await client.GetAsync("/Profile/Preferences");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{vinegarId}\" id=\"never-{vinegarId}\" checked", html);
        Assert.Contains("(1 selected)", html);
    }

    [Fact]
    public async Task SearchOverrides_TimeLimitAndPreferenceSwitch_ApplyToThisSearchOnly()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var save = await AuthHelper.PostFormAsync(
            client,
            "/Profile/Preferences",
            new[]
            {
                new KeyValuePair<string, string>("PreferredProteinId", ""),
                new KeyValuePair<string, string>("PreferredMethod", "Soup"),
                new KeyValuePair<string, string>("MaxCookTimeMinutes", "45"),
            },
            token))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        using var overridden = await SearchAsync(client, token, new
        {
            text = "chicken,",
            usePreferences = false,
            overrideMaxCookTime = true,
            maxCookTimeMinutes = (int?)null,
        });
        var root = overridden.RootElement;
        Assert.Equal(0, root.GetProperty("overTimeLimitCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("maxCookTimeMinutes").ValueKind);
        Assert.All(
            root.GetProperty("dishes").EnumerateArray(),
            dish => Assert.Empty(dish.GetProperty("preferenceBadges").EnumerateArray()));

        // The saved preferences are untouched: a plain search still uses them.
        using var plain = await SearchAsync(client, token);
        Assert.Equal(45, plain.RootElement.GetProperty("maxCookTimeMinutes").GetInt32());
    }

    [Fact]
    public async Task Guest_CanSetATimeLimitForOneSearch()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var json = await SearchAsync(client, token, new
        {
            text = "chicken,",
            overrideMaxCookTime = true,
            maxCookTimeMinutes = 40,
        });

        Assert.Equal(40, json.RootElement.GetProperty("maxCookTimeMinutes").GetInt32());
        Assert.True(json.RootElement.GetProperty("overTimeLimitCount").GetInt32() > 0);
    }

    [Fact]
    public async Task SearchPage_ShowsControlsStartingFromSavedPreferences()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (await AuthHelper.PostFormAsync(
            client,
            "/Profile/Preferences",
            new[]
            {
                new KeyValuePair<string, string>("PreferredProteinId", ""),
                new KeyValuePair<string, string>("PreferredMethod", "Fried"),
                new KeyValuePair<string, string>("MaxCookTimeMinutes", "35"),
            },
            token))
        {
        }

        using var page = await client.GetAsync("/");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Contains("<option value=\"35\" selected=\"selected\">35 min</option>", html);
        Assert.Contains("Rank by my preferences (Fried)", html);
    }

    [Fact]
    public async Task Guest_SearchHasNoPreferences()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var json = await SearchAsync(client, token);

        Assert.False(json.RootElement.GetProperty("preferencesApplied").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("overTimeLimitCount").GetInt32());
    }

    private static async Task<JsonDocument> SearchAsync(
        HttpClient client, string token, object? body = null)
    {
        using var response = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", body ?? new { text = "chicken," }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
