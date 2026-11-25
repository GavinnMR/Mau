using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Core.Users;
using Mau.Data.Identity;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class AdminDishesListTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AdminDishesListTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestsAndNormalUsers_AreRedirectedFromDishPages()
    {
        using var guest = AuthHelper.CreateClient(_factory);
        using var normal = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(normal);

        foreach (var url in new[] { "/Admin/Dishes", "/Admin/Dishes/Delete/1" })
        {
            using var guestResponse = await guest.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, guestResponse.StatusCode);
            Assert.Contains(
                "/Account/Login", guestResponse.Headers.Location?.ToString());

            using var normalResponse = await normal.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, normalResponse.StatusCode);
            Assert.Contains(
                "/Account/AccessDenied", normalResponse.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Index_ListsEveryDishWithIngredientCount()
    {
        using var client = await CreateAdminClientAsync();
        var catalog = GetCatalog();

        using var response = await client.GetAsync("/Admin/Dishes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(catalog.Dishes.Count, Regex.Matches(html, "data-id=\"").Count);

        foreach (var dish in catalog.Dishes)
        {
            var row = Regex.Match(
                html,
                $"<tr data-id=\"{dish.Id}\">(.*?)</tr>",
                RegexOptions.Singleline);
            Assert.True(row.Success, $"Missing row for {dish.Name}");
            Assert.Contains(dish.Name, row.Groups[1].Value);
            Assert.Contains($"<td>{dish.Ingredients.Count}</td>", row.Groups[1].Value);
        }
    }

    [Fact]
    public async Task Index_FilterByNameIgnoringCaseAndNoMatch()
    {
        using var client = await CreateAdminClientAsync();
        var expected = GetCatalog().Dishes
            .Where(dish => dish.Name.Contains("ADOBO", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(expected);

        using var response = await client.GetAsync("/Admin/Dishes?q=ADOBO");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected.Count, Regex.Matches(html, "data-id=\"").Count);
        foreach (var dish in expected)
        {
            Assert.Contains($"data-id=\"{dish.Id}\"", html);
        }

        using var emptyResponse = await client.GetAsync("/Admin/Dishes?q=zzzz-no-match");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Contains("No dishes found.", emptyHtml);
        Assert.Empty(Regex.Matches(emptyHtml, "data-id=\""));
    }

    [Fact]
    public async Task DeleteForm_ShowsNameAndNote_UnknownIdIsNotFound()
    {
        using var client = await CreateAdminClientAsync();
        var dish = GetCatalog().Dishes.First();

        using var response = await client.GetAsync($"/Admin/Dishes/Delete/{dish.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"Delete “{dish.Name}”?", html);
        Assert.Contains("This also removes it from users' favorites.", html);
        Assert.Contains("<form", html);

        using var missing = await client.GetAsync("/Admin/Dishes/Delete/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Item not found", await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_RemovesDishEverywhere()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("dish");
        var id = await CreateDishAsync(name);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using (var before = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text = "chicken," }, token))
        {
            using var beforeJson = JsonDocument.Parse(
                await before.Content.ReadAsStringAsync());
            Assert.Contains(
                beforeJson.RootElement.GetProperty("dishes").EnumerateArray(),
                dish => dish.GetProperty("name").GetString() == name);
        }

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Dishes/Delete/{id}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Dishes", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Admin/Dishes");
        Assert.Contains("Dish deleted.", await page.Content.ReadAsStringAsync());
        Assert.DoesNotContain(GetCatalog().Dishes, dish => dish.Id == id);

        using var details = await client.GetAsync($"/Dish/Details/{id}");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);

        using var after = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text = "chicken," }, token);
        using var afterJson = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            afterJson.RootElement.GetProperty("dishes").EnumerateArray(),
            dish => dish.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Delete_RemovesFavorite()
    {
        using var client = await CreateAdminClientAsync();
        var id = await CreateDishAsync(Unique("favdish"));

        using var userClient = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(userClient);
        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            userId = user!.Id;
        }

        var favorites = _factory.Services.GetRequiredService<IFavoriteService>();
        Assert.True(await favorites.AddAsync(userId, id));
        var favoriteStore = _factory.Services.GetRequiredService<IFavoriteStore>();
        Assert.Contains(id, await favoriteStore.GetDishIdsAsync(userId));

        var token = await AuthHelper.GetCsrfTokenAsync(client);
        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Dishes/Delete/{id}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.Empty(await favoriteStore.GetDishIdsAsync(userId));
    }

    [Fact]
    public async Task PostWithoutToken_ReturnsBadRequest()
    {
        using var client = await CreateAdminClientAsync();
        var dish = GetCatalog().Dishes.First();

        using var response = await client.PostAsync(
            $"/Admin/Dishes/Delete/{dish.Id}",
            new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Index_HtmlEncodesDishNames()
    {
        using var client = await CreateAdminClientAsync();
        await CreateDishAsync($"<b>x</b>-{Unique("markup")}");

        using var response = await client.GetAsync("/Admin/Dishes");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.LoginAsAdminAsync(client);
        return client;
    }

    private static string Unique(string prefix) =>
        $"zz-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<int> CreateDishAsync(string name)
    {
        var chickenId = GetCatalog().Ingredients
            .Single(ingredient => ingredient.CanonicalName == "chicken").Id;

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogAdminService>();
        var result = await service.CreateDishAsync(new DishInput(
            name,
            "Description.",
            CookingMethod.Fried,
            10,
            new[] { "Step." },
            new[] { new DishIngredientInput(chickenId, 1m, "kg", false) }));

        Assert.True(result.Succeeded);
        return result.Id!.Value;
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;
}
