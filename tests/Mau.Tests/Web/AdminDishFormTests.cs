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

public class AdminDishFormTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AdminDishFormTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestsAndNormalUsers_AreRedirectedFromDishFormPages()
    {
        using var guest = AuthHelper.CreateClient(_factory);
        using var normal = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(normal);
        var fields = DishFields(
            Unique("blocked"),
            rows: new[] { new Row(IngredientId("chicken").ToString(), "1", "kg") });

        foreach (var url in new[] { "/Admin/Dishes/Create", "/Admin/Dishes/Edit/1" })
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

        var guestToken = await AuthHelper.GetCsrfTokenAsync(guest);
        using var guestPost = await AuthHelper.PostFormAsync(
            guest, "/Admin/Dishes/Create", fields, guestToken);
        Assert.Equal(HttpStatusCode.Redirect, guestPost.StatusCode);

        var normalToken = await AuthHelper.GetCsrfTokenAsync(normal);
        using var normalPost = await AuthHelper.PostFormAsync(
            normal, "/Admin/Dishes/Create", fields, normalToken);
        Assert.Equal(HttpStatusCode.Redirect, normalPost.StatusCode);
    }

    [Fact]
    public async Task CreateForm_HasEightBlankRowsOptionsStepsAndMethod()
    {
        using var client = await CreateAdminClientAsync();
        var catalog = GetCatalog();

        using var response = await client.GetAsync("/Admin/Dishes/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ingredients[0].IngredientId", html);
        Assert.Contains("Ingredients[7].IngredientId", html);
        Assert.DoesNotContain("Ingredients[8].IngredientId", html);

        var select = Regex.Match(
            html,
            "<select[^>]*name=\"Ingredients\\[0\\]\\.IngredientId\"[^>]*>(.*?)</select>",
            RegexOptions.Singleline);
        Assert.True(select.Success);
        Assert.Equal(
            catalog.Ingredients.Count + 1,
            Regex.Matches(select.Groups[1].Value, "<option").Count);

        Assert.Contains("name=\"Steps\"", html);
        Assert.Contains("name=\"CookingMethod\"", html);
    }

    [Fact]
    public async Task Create_ValidDish_SavesAndShowsOnDetailsAndSearch()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("dish");
        var chickenId = IngredientId("chicken");
        var garlicId = IngredientId("garlic");
        var saltId = IngredientId("salt");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Dishes/Create",
            DishFields(
                name,
                "Dish description.",
                "Soup",
                "30",
                "Boil the water.\nAdd the shrimp.",
                new Row(chickenId.ToString(), "1", "kg"),
                new Row(garlicId.ToString(), "0.5", "kg"),
                new Row(saltId.ToString(), "", "", "true")),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Dishes", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Admin/Dishes");
        Assert.Contains("Dish saved.", await page.Content.ReadAsStringAsync());

        var dish = GetCatalog().Dishes.SingleOrDefault(candidate => candidate.Name == name);
        Assert.NotNull(dish);
        Assert.Equal(3, dish!.Ingredients.Count);

        using var details = await client.GetAsync($"/Dish/Details/{dish.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains("Boil the water.", detailsHtml);
        Assert.Contains("Add the shrimp.", detailsHtml);
        Assert.Contains("0.5 kg", detailsHtml);

        using var search = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text = "chicken," }, token);
        using var json = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Contains(
            json.RootElement.GetProperty("dishes").EnumerateArray(),
            candidate => candidate.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Create_BlankRowsAreIgnored()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("blank");
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var rows = new List<Row>
        {
            new(IngredientId("chicken").ToString(), "1", "kg"),
        };
        rows.AddRange(Enumerable.Range(0, 5).Select(_ => new Row("")));

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Dishes/Create",
            DishFields(name, rows: rows.ToArray()),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var dish = GetCatalog().Dishes.SingleOrDefault(candidate => candidate.Name == name);
        Assert.NotNull(dish);
        Assert.Single(dish!.Ingredients);
    }

    [Fact]
    public async Task Create_InvalidInputs_ShowExactMessages()
    {
        using var client = await CreateAdminClientAsync();
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var chickenId = IngredientId("chicken");

        async Task AssertCreateErrorAsync(
            IEnumerable<KeyValuePair<string, string>> fields, string message)
        {
            using var response = await AuthHelper.PostFormAsync(
                client, "/Admin/Dishes/Create", fields, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(message, await response.Content.ReadAsStringAsync());
        }

        await AssertCreateErrorAsync(
            DishFields("", rows: new[] { new Row(chickenId.ToString(), "1", "kg") }),
            "Name is required.");
        await AssertCreateErrorAsync(
            DishFields("Chicken adobo", rows: new[] { new Row(chickenId.ToString(), "1", "kg") }),
            "A dish with that name already exists.");
        await AssertCreateErrorAsync(
            DishFields(
                Unique("dish"), cookTime: "0",
                rows: new[] { new Row(chickenId.ToString(), "1", "kg") }),
            "Cooking time must be between 1 and 600 minutes.");
        await AssertCreateErrorAsync(
            DishFields(
                Unique("dish"), steps: "",
                rows: new[] { new Row(chickenId.ToString(), "1", "kg") }),
            "Add at least one step.");
        await AssertCreateErrorAsync(
            DishFields(
                Unique("dish"),
                rows: new[] { new Row(chickenId.ToString(), "", "kg") }),
            "Enter a quantity or clear the unit.");
        await AssertCreateErrorAsync(
            DishFields(
                Unique("dish"),
                rows: new[]
                {
                    new Row(chickenId.ToString(), "1", "kg"),
                    new Row(chickenId.ToString(), "1", "kg"),
                }),
            "chicken is listed more than once.");
        await AssertCreateErrorAsync(
            DishFields(
                Unique("dish"),
                rows: new[] { new Row(chickenId.ToString(), "1", "kg", "true") }),
            "A dish needs at least one required ingredient.");
    }

    [Fact]
    public async Task Create_FailedPost_KeepsTypedRowsAndAddsBlankRows()
    {
        using var client = await CreateAdminClientAsync();
        var chickenId = IngredientId("chicken");
        var garlicId = IngredientId("garlic");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Dishes/Create",
            DishFields(
                "",
                "Typed description.",
                "Fried",
                "25",
                "Typed step.",
                new Row(chickenId.ToString(), "1", "kg"),
                new Row(garlicId.ToString(), "", "", "true")),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name is required.", html);
        Assert.Matches(
            $"(value=\"{chickenId}\"[^>]*selected|selected[^>]*value=\"{chickenId}\")", html);
        Assert.Matches(
            $"(value=\"{garlicId}\"[^>]*selected|selected[^>]*value=\"{garlicId}\")", html);
        Assert.Matches("name=\"Ingredients\\[0\\]\\.Quantity\"[^>]*value=\"1\"", html);
        Assert.Matches("name=\"Ingredients\\[0\\]\\.Unit\"[^>]*value=\"kg\"", html);
        Assert.Contains("Typed description.", html);
        Assert.Contains("Typed step.", html);

        var rowCount = Regex.Matches(html, "Ingredients\\[(\\d+)\\]\\.IngredientId")
            .Select(match => int.Parse(match.Groups[1].Value))
            .Distinct()
            .Count();
        Assert.True(rowCount >= 5, $"Expected at least 5 ingredient rows, found {rowCount}.");
    }

    [Fact]
    public async Task EditForm_IsPrefilledWithRowsAndSteps()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("edit");
        var chickenId = IngredientId("chicken");
        var garlicId = IngredientId("garlic");
        var id = await CreateDishAsync(
            name,
            "Edit description.",
            CookingMethod.Fried,
            25,
            new[] { "First step.", "Second step." },
            new DishIngredientInput(chickenId, 0.5m, "kg", false),
            new DishIngredientInput(garlicId, null, null, true));

        using var response = await client.GetAsync($"/Admin/Dishes/Edit/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{name}\"", html);
        Assert.Contains("Edit description.", html);
        Assert.Contains("value=\"25\"", html);
        Assert.Matches(
            "First step\\.\\s*Second step\\.",
            WebUtility.HtmlDecode(html));
        Assert.Matches(
            $"(value=\"{chickenId}\"[^>]*selected|selected[^>]*value=\"{chickenId}\")", html);
        Assert.Matches(
            $"(value=\"{garlicId}\"[^>]*selected|selected[^>]*value=\"{garlicId}\")", html);

        var rowCount = Regex.Matches(html, "Ingredients\\[(\\d+)\\]\\.IngredientId")
            .Select(match => int.Parse(match.Groups[1].Value))
            .Distinct()
            .Count();
        Assert.True(rowCount >= 5, $"Expected at least 5 ingredient rows, found {rowCount}.");
    }

    [Fact]
    public async Task Edit_ChangesDishAndKeepsFavorite()
    {
        using var client = await CreateAdminClientAsync();
        var originalName = Unique("editold");
        var porkId = IngredientId("pork");
        var onionId = IngredientId("onion");
        var id = await CreateDishAsync(
            originalName,
            "Old description.",
            CookingMethod.Soup,
            30,
            new[] { "Old step." },
            new DishIngredientInput(IngredientId("chicken"), 1m, "kg", false));
        var (userId, favoriteStore) = await CreateUserWithFavoriteAsync(id);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var newName = Unique("editnew");

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Dishes/Edit/{id}",
            DishFields(
                newName,
                "New description.",
                "Fried",
                "40",
                "New step.\nSecond new step.",
                new Row(porkId.ToString(), "1.5", "kg"),
                new Row(onionId.ToString(), "", "", "true")),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Dishes", response.Headers.Location?.ToString());

        var dish = GetCatalog().DishesById.GetValueOrDefault(id);
        Assert.NotNull(dish);
        Assert.Equal(newName, dish!.Name);

        using var details = await client.GetAsync($"/Dish/Details/{id}");
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains(newName, detailsHtml);
        Assert.Contains("New description.", detailsHtml);
        Assert.Contains("New step.", detailsHtml);
        Assert.Contains("1.5 kg", detailsHtml);
        Assert.Contains(id, await favoriteStore.GetDishIdsAsync(userId));
    }

    [Fact]
    public async Task Edit_KeepingSameName_Succeeds()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("samedish");
        var chickenId = IngredientId("chicken");
        var id = await CreateDishAsync(
            name,
            "Description.",
            CookingMethod.Soup,
            30,
            new[] { "Step." },
            new DishIngredientInput(chickenId, 1m, "kg", false));
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Dishes/Edit/{id}",
            DishFields(
                name,
                "Description.",
                "Soup",
                "30",
                "Step.",
                new Row(chickenId.ToString(), "1", "kg")),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(name, GetCatalog().DishesById[id].Name);
    }

    [Fact]
    public async Task Edit_UnknownId_ReturnsAdminNotFound()
    {
        using var client = await CreateAdminClientAsync();

        using var response = await client.GetAsync("/Admin/Dishes/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Item not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostWithoutToken_ReturnsBadRequest()
    {
        using var client = await CreateAdminClientAsync();
        var fields = DishFields(
            Unique("notoken"),
            rows: new[] { new Row(IngredientId("chicken").ToString(), "1", "kg") });

        using var response = await client.PostAsync(
            "/Admin/Dishes/Create", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Details_HtmlEncodesDescriptionMarkup()
    {
        using var client = await CreateAdminClientAsync();
        var id = await CreateDishAsync(Unique("markup"), "<b>bold</b>");

        using var response = await client.GetAsync($"/Dish/Details/{id}");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;b&gt;bold&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>bold</b>", html);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.LoginAsAdminAsync(client);
        return client;
    }

    private static string Unique(string prefix) =>
        $"zz-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private int IngredientId(string name) =>
        GetCatalog().Ingredients.Single(ingredient => ingredient.CanonicalName == name).Id;

    private async Task<int> CreateDishAsync(
        string name,
        string description = "Description.",
        CookingMethod method = CookingMethod.Soup,
        int cookTime = 30,
        string[]? steps = null,
        params DishIngredientInput[] rows)
    {
        var effectiveRows = rows.Length > 0
            ? rows
            : new[] { new DishIngredientInput(IngredientId("chicken"), 1m, "kg", false) };

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogAdminService>();
        var result = await service.CreateDishAsync(new DishInput(
            name,
            description,
            method,
            cookTime,
            steps ?? new[] { "Step." },
            effectiveRows));

        Assert.True(result.Succeeded);
        return result.Id!.Value;
    }

    private async Task<(string UserId, IFavoriteStore Store)> CreateUserWithFavoriteAsync(
        int dishId)
    {
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
        Assert.True(await favorites.AddAsync(userId, dishId));
        return (userId, _factory.Services.GetRequiredService<IFavoriteStore>());
    }

    private static List<KeyValuePair<string, string>> DishFields(
        string name,
        string description = "Description.",
        string method = "Soup",
        string cookTime = "30",
        string steps = "Step.",
        params Row[] rows)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Name", name),
            new("Description", description),
            new("CookingMethod", method),
            new("CookTimeMinutes", cookTime),
            new("Steps", steps),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            fields.Add(new($"Ingredients[{i}].IngredientId", rows[i].IngredientId));
            fields.Add(new($"Ingredients[{i}].Quantity", rows[i].Quantity));
            fields.Add(new($"Ingredients[{i}].Unit", rows[i].Unit));
            fields.Add(new($"Ingredients[{i}].IsOptional", rows[i].IsOptional));
        }

        return fields;
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;

    private sealed record Row(
        string IngredientId, string Quantity = "", string Unit = "", string IsOptional = "false");
}
