using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class FavoritesTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public FavoritesTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Guest_PutFavorite_ReturnsUnauthorizedJson()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PutAsync(client, dishId, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("Please log in.", errors);
    }

    [Fact]
    public async Task LoggedIn_PutFavorite_ReturnsOkAndDetailsShowsFavorite()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await PutAsync(client, dishId, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());

        using var details = await client.GetAsync($"/Dish/Details/{dishId}");
        var html = await details.Content.ReadAsStringAsync();
        Assert.Contains("data-favorite=\"true\"", html);
        Assert.Contains("In your favorites", html);
    }

    [Fact]
    public async Task Guest_DetailsPage_ShowsLoginToSaveFavoritesLink()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;

        using var response = await client.GetAsync($"/Dish/Details/{dishId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Log in to save favorites", html);
        Assert.Contains($"returnUrl=%2FDish%2FDetails%2F{dishId}", html);
    }

    [Fact]
    public async Task LoggedIn_PutTwice_ListsDishOnce()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using (var first = await PutAsync(client, dishId, token))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var second = await PutAsync(client, dishId, token))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        using var page = await client.GetAsync("/Profile/Favorites");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Single(Regex.Matches(html, Regex.Escape($"/Dish/Details/{dishId}")));
    }

    [Fact]
    public async Task LoggedIn_DeleteFavorite_ReturnsOkAndDetailsShowsNotFavorite()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var add = await PutAsync(client, dishId, token))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        using var response = await DeleteAsync(client, dishId, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var details = await client.GetAsync($"/Dish/Details/{dishId}");
        Assert.Contains(
            "data-favorite=\"false\"",
            await details.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoggedIn_PutAndDeleteUnknownDish_ReturnNotFound()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var put = await PutAsync(client, 99999, token);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Contains("Dish not found.", await ReadErrorsAsync(put));

        using var delete = await DeleteAsync(client, 99999, token);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Contains("Dish not found.", await ReadErrorsAsync(delete));
    }

    [Fact]
    public async Task LoggedIn_PutWithoutToken_ReturnsBadRequest()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;

        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/favorites/{dishId}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Guest_GetFavorites_RedirectsToLogin()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Profile/Favorites");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoggedIn_NoFavorites_ShowsEmptyState()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/Profile/Favorites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No favorites yet.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoggedIn_TwoFavorites_ListedNewestFirst()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishes = GetCatalog().Dishes.OrderBy(dish => dish.Id).Take(2).ToArray();
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using (var first = await PutAsync(client, dishes[0].Id, token))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var second = await PutAsync(client, dishes[1].Id, token))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        using var page = await client.GetAsync("/Profile/Favorites");
        var html = await page.Content.ReadAsStringAsync();
        var newerIndex = html.IndexOf($"/Dish/Details/{dishes[1].Id}");
        var olderIndex = html.IndexOf($"/Dish/Details/{dishes[0].Id}");

        Assert.True(newerIndex >= 0);
        Assert.True(olderIndex >= 0);
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task LoggedIn_RemoveFavorite_RedirectsAndShowsMessageOnce()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using (var add = await PutAsync(client, dishId, token))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Profile/RemoveFavorite/{dishId}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/Favorites", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Profile/Favorites");
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain($"/Dish/Details/{dishId}", html);
        Assert.Contains("Removed from favorites.", html);

        using var second = await client.GetAsync("/Profile/Favorites");
        Assert.DoesNotContain(
            "Removed from favorites.", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TwoUsers_FavoritesAreIsolated()
    {
        using var userA = AuthHelper.CreateClient(_factory);
        using var userB = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(userA);
        await AuthHelper.RegisterAsync(userB);
        var dishId = FindDish(GetCatalog(), "Chicken adobo").Id;
        var token = await AuthHelper.GetCsrfTokenAsync(userA);

        using (var add = await PutAsync(userA, dishId, token))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        using var pageB = await userB.GetAsync("/Profile/Favorites");
        Assert.Contains("No favorites yet.", await pageB.Content.ReadAsStringAsync());
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;

    private static CatalogDish FindDish(CatalogSnapshot catalog, string name) =>
        catalog.Dishes.Single(dish => dish.Name == name);

    private static Task<HttpResponseMessage> PutAsync(
        HttpClient client, int dishId, string token) =>
        AuthHelper.SendJsonAsync(
            client, HttpMethod.Put, $"/api/favorites/{dishId}", null, token);

    private static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client, int dishId, string token) =>
        AuthHelper.SendJsonAsync(
            client, HttpMethod.Delete, $"/api/favorites/{dishId}", null, token);

    private static async Task<string[]> ReadErrorsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .ToArray();
    }
}
