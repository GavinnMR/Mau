using System.Net;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mau.Tests.Web;

public class PageTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public PageTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHome_ReturnsSearchPageWithCsrfToken()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"ingredient-input\"", html);
        Assert.Contains("name=\"csrf-token\"", html);
    }

    [Fact]
    public async Task GetDishDetails_ExistingId_ReturnsDishPage()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/Dish/Details/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Chicken adobo", html);
        Assert.Contains("<ol", html);
    }

    [Fact]
    public async Task GetDishDetails_UnknownId_ReturnsNotFoundPage()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/Dish/Details/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dish not found", html);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
}
