using System.Net;
using System.Text.Json;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class AdminEndToEndTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AdminEndToEndTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewIngredientAndDish_AppearInSearchHintsAndPantry()
    {
        using var admin = await CreateAdminClientAsync();
        var adminToken = await AuthHelper.GetCsrfTokenAsync(admin);
        var suffix = Suffix();
        var ingredientName = $"zz-e2e-ing-{suffix}";
        var synonym = $"zzse{suffix}";
        var ingredient = await CreateIngredientAsync(
            admin, adminToken, ingredientName, synonym);
        var chickenId = IngredientId("chicken");
        var dishName = $"zz-e2e-dish-{suffix}";
        var dish = await CreateDishAsync(
            admin,
            adminToken,
            dishName,
            new Row(ingredient.Id.ToString(), "1", "kg"),
            new Row(chickenId.ToString(), "1", "kg"));

        using var user = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(user);
        var userToken = await AuthHelper.GetCsrfTokenAsync(user);

        using var search = await AuthHelper.SendJsonAsync(
            user, HttpMethod.Post, "/api/search", new { text = $"{synonym}," }, userToken);
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Contains(
            searchJson.RootElement.GetProperty("dishes").EnumerateArray(),
            candidate => candidate.GetProperty("name").GetString() == dish.Name);

        var prefix = synonym[..3];
        using var hints = await user.GetAsync($"/api/hints?prefix={prefix}");
        Assert.Contains(synonym, await hints.Content.ReadAsStringAsync());

        using var pantry = await user.GetAsync("/Profile/Pantry");
        var pantryHtml = await pantry.Content.ReadAsStringAsync();
        Assert.Contains(ingredientName, pantryHtml);
        Assert.Contains($"value=\"{ingredient.Id}\"", pantryHtml);
    }

    [Fact]
    public async Task RenamedDish_SearchUsesNewNameAndNotOldName()
    {
        using var admin = await CreateAdminClientAsync();
        var adminToken = await AuthHelper.GetCsrfTokenAsync(admin);
        var suffix = Suffix();
        var oldName = $"zz-e2e-old-{suffix}";
        var newName = $"zz-e2e-new-{suffix}";
        var chickenId = IngredientId("chicken");
        var dish = await CreateDishAsync(
            admin, adminToken, oldName, new Row(chickenId.ToString(), "1", "kg"));

        using var user = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(user);
        var userToken = await AuthHelper.GetCsrfTokenAsync(user);
        using (var before = await AuthHelper.SendJsonAsync(
            user, HttpMethod.Post, "/api/search", new { text = "chicken," }, userToken))
        {
            using var beforeJson = JsonDocument.Parse(
                await before.Content.ReadAsStringAsync());
            Assert.Contains(
                beforeJson.RootElement.GetProperty("dishes").EnumerateArray(),
                candidate => candidate.GetProperty("name").GetString() == oldName);
        }

        using (var response = await AuthHelper.PostFormAsync(
            admin,
            $"/Admin/Dishes/Edit/{dish.Id}",
            DishFields(
                newName,
                "Renamed description.",
                "Soup",
                "35",
                "Renamed step.",
                new Row(chickenId.ToString(), "1", "kg")),
            adminToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var after = await AuthHelper.SendJsonAsync(
            user, HttpMethod.Post, "/api/search", new { text = "chicken," }, userToken);
        using var afterJson = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        var dishes = afterJson.RootElement.GetProperty("dishes").EnumerateArray();
        Assert.Contains(
            dishes,
            candidate => candidate.GetProperty("name").GetString() == newName);
        Assert.DoesNotContain(
            dishes,
            candidate => candidate.GetProperty("name").GetString() == oldName);
    }

    [Fact]
    public async Task RemovedSynonym_IsNoLongerRecognised()
    {
        using var admin = await CreateAdminClientAsync();
        var adminToken = await AuthHelper.GetCsrfTokenAsync(admin);
        var suffix = Suffix();
        var ingredientName = $"zz-e2e-syn-{suffix}";
        var synonym = $"zzsy{suffix}";
        var ingredient = await CreateIngredientAsync(
            admin, adminToken, ingredientName, synonym);
        var category = ingredient.Category.ToString();

        using var user = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(user);
        var userToken = await AuthHelper.GetCsrfTokenAsync(user);
        using (var before = await AuthHelper.SendJsonAsync(
            user, HttpMethod.Post, "/api/search", new { text = $"{synonym}," }, userToken))
        {
            using var beforeJson = JsonDocument.Parse(
                await before.Content.ReadAsStringAsync());
            Assert.Equal(
                ingredient.Id,
                beforeJson.RootElement.GetProperty("tokens")[0]
                    .GetProperty("ingredientId").GetInt32());
        }

        using (var edit = await AuthHelper.PostFormAsync(
            admin,
            $"/Admin/Ingredients/Edit/{ingredient.Id}",
            new[]
            {
                new KeyValuePair<string, string>("Name", ingredientName),
                new KeyValuePair<string, string>("Category", category),
                new KeyValuePair<string, string>(
                    "IsStaple", ingredient.IsStaple ? "true" : "false"),
                new KeyValuePair<string, string>("Synonyms[0].Name", ""),
            },
            adminToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        }

        using var after = await AuthHelper.SendJsonAsync(
            user, HttpMethod.Post, "/api/search", new { text = $"{synonym}," }, userToken);
        using var afterJson = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Null,
            afterJson.RootElement.GetProperty("tokens")[0]
                .GetProperty("ingredientId").ValueKind);
    }

    [Fact]
    public async Task DeleteIngredient_BlockedByDish_ThenSucceedsAfterDishDelete()
    {
        using var admin = await CreateAdminClientAsync();
        var adminToken = await AuthHelper.GetCsrfTokenAsync(admin);
        var suffix = Suffix();
        var ingredientName = $"zz-e2e-del-{suffix}";
        var ingredient = await CreateIngredientAsync(
            admin, adminToken, ingredientName, $"zzsd{suffix}");
        var chickenId = IngredientId("chicken");
        var dishName = $"zz-e2e-del-dish-{suffix}";
        var dish = await CreateDishAsync(
            admin,
            adminToken,
            dishName,
            new Row(ingredient.Id.ToString(), "1", "kg"),
            new Row(chickenId.ToString(), "1", "kg"));

        using (var blocked = await AuthHelper.PostFormAsync(
            admin,
            $"/Admin/Ingredients/Delete/{ingredient.Id}",
            Array.Empty<KeyValuePair<string, string>>(),
            adminToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        }

        using (var list = await admin.GetAsync("/Admin/Ingredients"))
        {
            var html = WebUtility.HtmlDecode(await list.Content.ReadAsStringAsync());
            Assert.Contains($"Cannot delete “{ingredientName}”", html);
            Assert.Contains(dishName, html);
        }

        using (var deleteDish = await AuthHelper.PostFormAsync(
            admin,
            $"/Admin/Dishes/Delete/{dish.Id}",
            Array.Empty<KeyValuePair<string, string>>(),
            adminToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteDish.StatusCode);
        }

        Assert.DoesNotContain(GetCatalog().Dishes, candidate => candidate.Id == dish.Id);

        using (var deleteIngredient = await AuthHelper.PostFormAsync(
            admin,
            $"/Admin/Ingredients/Delete/{ingredient.Id}",
            Array.Empty<KeyValuePair<string, string>>(),
            adminToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteIngredient.StatusCode);
        }

        Assert.DoesNotContain(
            GetCatalog().Ingredients, candidate => candidate.Id == ingredient.Id);

        using var finalList = await admin.GetAsync("/Admin/Ingredients");
        Assert.Contains(
            "Ingredient deleted.", await finalList.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NormalUser_CannotOpenAdminPages()
    {
        using var user = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(user);

        foreach (var url in new[]
        {
            "/Admin",
            "/Admin/Ingredients",
            "/Admin/Ingredients/Create",
            "/Admin/Ingredients/Edit/1",
            "/Admin/Dishes",
            "/Admin/Dishes/Create",
            "/Admin/Dishes/Edit/1",
        })
        {
            using var response = await user.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(
                "/Account/AccessDenied", response.Headers.Location?.ToString());
        }
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.LoginAsAdminAsync(client);
        return client;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private async Task<CatalogIngredient> CreateIngredientAsync(
        HttpClient admin, string token, string name, string synonym)
    {
        using var response = await AuthHelper.PostFormAsync(
            admin,
            "/Admin/Ingredients/Create",
            new[]
            {
                new KeyValuePair<string, string>("Name", name),
                new KeyValuePair<string, string>("Category", "Other"),
                new KeyValuePair<string, string>("IsStaple", "false"),
                new KeyValuePair<string, string>("Synonyms[0].Name", synonym),
                new KeyValuePair<string, string>("Synonyms[0].Language", "Filipino"),
            },
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return GetCatalog().Ingredients.Single(
            ingredient => ingredient.CanonicalName == name);
    }

    private async Task<CatalogDish> CreateDishAsync(
        HttpClient admin, string token, string name, params Row[] rows)
    {
        using var response = await AuthHelper.PostFormAsync(
            admin,
            "/Admin/Dishes/Create",
            DishFields(name, rows: rows),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return GetCatalog().Dishes.Single(dish => dish.Name == name);
    }

    private int IngredientId(string name) =>
        GetCatalog().Ingredients.Single(ingredient => ingredient.CanonicalName == name).Id;

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
