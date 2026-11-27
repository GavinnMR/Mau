using System.Net;
using System.Text.Json;
using Mau.Core.Catalog;
using Mau.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class RankingApiTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public RankingApiTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_ReturnsScoresThatAddUpToOne()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        var dishes = await SearchAsync(client, "chicken,", token);

        Assert.NotEmpty(dishes);
        Assert.Equal(1.0, dishes.Sum(dish => dish.Score), 2);
        Assert.Equal(dishes.OrderByDescending(dish => dish.Score).Select(dish => dish.Name),
            dishes.Select(dish => dish.Name));
    }

    [Fact]
    public async Task CookedFeedback_MovesDishToTheTop()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);
        var before = await SearchAsync(client, "chicken,", token);
        var last = before[^1];
        Assert.NotEqual(last.Name, before[0].Name);

        var dishId = _factory.Services.GetRequiredService<CatalogStore>().Current.DishesById.Values
            .Single(dish => dish.Name == last.Name).Id;
        for (var i = 0; i < 5; i++)
        {
            var search = await SearchHelper.GetSearchTokenAsync(client, token);
            using var response = await AuthHelper.SendJsonAsync(
                client, HttpMethod.Post, "/api/feedback", new { dishId, type = "cooked", search }, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var after = await SearchAsync(client, "chicken,", token);

        Assert.Equal(last.Name, after[0].Name);
    }

    private static async Task<List<(string Name, double Score)>> SearchAsync(
        HttpClient client, string text, string token)
    {
        using var response = await AuthHelper.SendJsonAsync(
            client, HttpMethod.Post, "/api/search", new { text }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("dishes").EnumerateArray()
            .Select(dish => (dish.GetProperty("name").GetString()!, dish.GetProperty("score").GetDouble()))
            .ToList();
    }
}
