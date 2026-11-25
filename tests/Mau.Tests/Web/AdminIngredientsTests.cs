using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class AdminIngredientsTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AdminIngredientsTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestsAndNormalUsers_AreRedirectedFromIngredientPages()
    {
        using var guest = AuthHelper.CreateClient(_factory);
        using var normal = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(normal);

        foreach (var url in new[]
        {
            "/Admin/Ingredients",
            "/Admin/Ingredients/Create",
            "/Admin/Ingredients/Edit/1",
            "/Admin/Ingredients/Delete/1",
        })
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
            guest, "/Admin/Ingredients/Create", CreateFields(Unique("blocked")), guestToken);
        Assert.Equal(HttpStatusCode.Redirect, guestPost.StatusCode);

        var normalToken = await AuthHelper.GetCsrfTokenAsync(normal);
        using var normalPost = await AuthHelper.PostFormAsync(
            normal, "/Admin/Ingredients/Create", CreateFields(Unique("blocked")), normalToken);
        Assert.Equal(HttpStatusCode.Redirect, normalPost.StatusCode);
    }

    [Fact]
    public async Task Index_ListsEveryIngredientAndChickenUsage()
    {
        using var client = await CreateAdminClientAsync();
        var catalog = GetCatalog();
        var chicken = FindIngredient(catalog, "chicken");
        var usedBy = catalog.DishIdsByIngredientId.TryGetValue(chicken.Id, out var dishIds)
            ? dishIds.Count
            : 0;

        using var response = await client.GetAsync("/Admin/Ingredients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(
            catalog.Ingredients.Count, Regex.Matches(html, "data-id=\"").Count);
        Assert.Contains($"{usedBy} dishes", html);
    }

    [Fact]
    public async Task Index_FilterBySynonymAndByNoMatch()
    {
        using var client = await CreateAdminClientAsync();

        using var synonymResponse = await client.GetAsync("/Admin/Ingredients?q=toyo");
        var synonymHtml = await synonymResponse.Content.ReadAsStringAsync();
        Assert.Single(Regex.Matches(synonymHtml, "data-id=\""));
        Assert.Contains("soy sauce", synonymHtml);

        using var emptyResponse = await client.GetAsync("/Admin/Ingredients?q=zzzz-no-match");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Contains("No ingredients found.", emptyHtml);
        Assert.Empty(Regex.Matches(emptyHtml, "data-id=\""));
    }

    [Fact]
    public async Task CreateForm_HasThreeSynonymRows()
    {
        using var client = await CreateAdminClientAsync();

        using var response = await client.GetAsync("/Admin/Ingredients/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Synonyms[0].Name", html);
        Assert.Contains("Synonyms[1].Name", html);
        Assert.Contains("Synonyms[2].Name", html);
        Assert.DoesNotContain("Synonyms[3].Name", html);
    }

    [Fact]
    public async Task Create_ValidIngredient_SavesAndAppearsInCatalogAndHints()
    {
        using var client = await CreateAdminClientAsync();
        var name = Unique("ing");
        var synonym = Unique("syn");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Ingredients/Create",
            CreateFields(name, synonym),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Ingredients", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Admin/Ingredients");
        Assert.Contains("Ingredient saved.", await page.Content.ReadAsStringAsync());

        var created = GetCatalog().Ingredients.SingleOrDefault(
            ingredient => ingredient.CanonicalName == name);
        Assert.NotNull(created);
        Assert.Contains(created!.SynonymItems, item => item.Name == synonym);

        var prefix = synonym[..Math.Min(3, synonym.Length)];
        using var hints = await client.GetAsync($"/api/hints?prefix={prefix}");
        Assert.Contains(synonym, await hints.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_EmptyName_ShowsErrorAndKeepsTypedSynonym()
    {
        using var client = await CreateAdminClientAsync();
        var synonym = Unique("syn");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Ingredients/Create",
            CreateFields("", synonym),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name is required.", html);
        Assert.Contains(synonym, html);
    }

    [Fact]
    public async Task Create_DuplicateName_ShowsUsedByMessage()
    {
        using var client = await CreateAdminClientAsync();
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Ingredients/Create",
            CreateFields("Chicken", Unique("syn")),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "That name is already used by chicken.",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_CommaInSynonym_ShowsCommaMessage()
    {
        using var client = await CreateAdminClientAsync();
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Admin/Ingredients/Create",
            CreateFields(Unique("ing"), "bad,synonym"),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Synonym cannot contain a comma.",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EditForm_IsPrefilledAndHasFourSynonymRows()
    {
        using var client = await CreateAdminClientAsync();
        var salt = FindIngredient(GetCatalog(), "salt");

        using var response = await client.GetAsync($"/Admin/Ingredients/Edit/{salt.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("value=\"salt\"", html);
        Assert.Contains(">Seasoning</option>", html);
        Assert.Contains("checked", html);
        Assert.Contains("Synonyms[0].Name", html);
        Assert.Contains("Synonyms[3].Name", html);
        Assert.DoesNotContain("Synonyms[4].Name", html);
    }

    [Fact]
    public async Task Edit_RenamesAndReplacesSynonymsInTheLiveCatalog()
    {
        using var client = await CreateAdminClientAsync();
        var oldSynonym = Unique("oldsyn");
        var id = await CreateIngredientAsync(Unique("ing"), oldSynonym);
        var newName = Unique("renamed");
        var newSynonym = Unique("newsyn");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Ingredients/Edit/{id}",
            CreateFields(newName, newSynonym),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Ingredients", response.Headers.Location?.ToString());

        using var oldSearch = await AuthHelper.SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/search",
            new { text = $"{oldSynonym}," },
            token);
        using var oldJson = JsonDocument.Parse(await oldSearch.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Null,
            oldJson.RootElement.GetProperty("tokens")[0]
                .GetProperty("ingredientId").ValueKind);

        using var newSearch = await AuthHelper.SendJsonAsync(
            client,
            HttpMethod.Post,
            "/api/search",
            new { text = $"{newSynonym}," },
            token);
        using var newJson = JsonDocument.Parse(await newSearch.Content.ReadAsStringAsync());
        Assert.Equal(
            id,
            newJson.RootElement.GetProperty("tokens")[0]
                .GetProperty("ingredientId").GetInt32());
    }

    [Fact]
    public async Task Edit_UnknownId_ReturnsAdminNotFound()
    {
        using var client = await CreateAdminClientAsync();

        using var response = await client.GetAsync("/Admin/Ingredients/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Item not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteForm_ShowsButtonForUnusedAndBlocksUsedIngredient()
    {
        using var client = await CreateAdminClientAsync();
        var unusedId = await CreateIngredientAsync(Unique("unused"));
        var chicken = FindIngredient(GetCatalog(), "chicken");

        using var unusedPage = await client.GetAsync(
            $"/Admin/Ingredients/Delete/{unusedId}");
        var unusedHtml = await unusedPage.Content.ReadAsStringAsync();
        Assert.Contains("<form", unusedHtml);
        Assert.Contains(">Delete<", unusedHtml);
        Assert.DoesNotContain("Remove it from them first", unusedHtml);

        using var chickenPage = await client.GetAsync(
            $"/Admin/Ingredients/Delete/{chicken.Id}");
        var chickenHtml = await chickenPage.Content.ReadAsStringAsync();
        Assert.Contains(
            "This ingredient is used by these dishes. Remove it from them first:",
            chickenHtml);
        Assert.Contains("Chicken adobo", chickenHtml);
        Assert.Contains("Chicken tinola", chickenHtml);
        Assert.Contains("Garlic fried chicken", chickenHtml);
        Assert.DoesNotContain(">Delete<", chickenHtml);
    }

    [Fact]
    public async Task Delete_UnusedIngredient_RedirectsAndRemovesIt()
    {
        using var client = await CreateAdminClientAsync();
        var id = await CreateIngredientAsync(Unique("del"));
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Ingredients/Delete/{id}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Ingredients", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Admin/Ingredients");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Ingredient deleted.", html);
        Assert.DoesNotContain($"data-id=\"{id}\"", html);
        Assert.DoesNotContain(
            GetCatalog().Ingredients, ingredient => ingredient.Id == id);
    }

    [Fact]
    public async Task Delete_UsedIngredient_ShowsErrorBannerAndKeepsIt()
    {
        using var client = await CreateAdminClientAsync();
        var chicken = FindIngredient(GetCatalog(), "chicken");
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            $"/Admin/Ingredients/Delete/{chicken.Id}",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Ingredients", response.Headers.Location?.ToString());

        using var page = await client.GetAsync("/Admin/Ingredients");
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Cannot delete “chicken”", html);
        Assert.Contains($"data-id=\"{chicken.Id}\"", html);
        Assert.NotNull(GetCatalog().IngredientsById.GetValueOrDefault(chicken.Id));
    }

    [Fact]
    public async Task PostWithoutToken_ReturnsBadRequest()
    {
        using var client = await CreateAdminClientAsync();

        using var response = await client.PostAsync(
            "/Admin/Ingredients/Create",
            new FormUrlEncodedContent(CreateFields(Unique("notoken"))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Index_HtmlEncodesIngredientNames()
    {
        using var client = await CreateAdminClientAsync();
        await CreateIngredientAsync("<b>x</b>");

        using var response = await client.GetAsync("/Admin/Ingredients");

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

    private static IEnumerable<KeyValuePair<string, string>> CreateFields(
        string name, string? synonym = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Name", name),
            new("Category", "Other"),
            new("IsStaple", "false"),
        };

        if (synonym is not null)
        {
            fields.Add(new("Synonyms[0].Name", synonym));
            fields.Add(new("Synonyms[0].Language", "Filipino"));
        }

        return fields;
    }

    private async Task<int> CreateIngredientAsync(string name, string? synonym = null)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICatalogAdminService>();
        var result = await service.CreateIngredientAsync(new IngredientInput(
            name, IngredientCategory.Other, false,
            synonym is null
                ? Array.Empty<SynonymInput>()
                : new[] { new SynonymInput(synonym, SynonymLanguage.Filipino) }));
        Assert.True(result.Succeeded);
        return result.Id!.Value;
    }

    private CatalogSnapshot GetCatalog() =>
        _factory.Services.GetRequiredService<CatalogStore>().Current;

    private static CatalogIngredient FindIngredient(
        CatalogSnapshot catalog, string name) =>
        catalog.Ingredients.Single(ingredient => ingredient.CanonicalName == name);
}
