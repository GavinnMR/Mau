using Mau.Data;
using Mau.Data.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Support;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private TestDatabase(string path, ServiceProvider provider)
    {
        Path = path;
        _provider = provider;
    }

    public string Path { get; }

    public IDbContextFactory<MauDbContext> ContextFactory =>
        _provider.GetRequiredService<IDbContextFactory<MauDbContext>>();

    public static async Task<TestDatabase> CreateAsync()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"mau-user-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContextFactory<MauDbContext>(options =>
            options.UseSqlite($"Data Source={path}"));
        var provider = services.BuildServiceProvider();

        var database = new TestDatabase(path, provider);
        await DatabaseInitializer.InitializeAsync(provider);
        return database;
    }

    public Task<MauDbContext> CreateContextAsync() => ContextFactory.CreateDbContextAsync();

    public async Task CreateUserAsync(string id)
    {
        await using var context = await CreateContextAsync();
        context.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
        });
        await context.SaveChangesAsync();
    }

    public async Task<int[]> GetIngredientIdsAsync(int count)
    {
        await using var context = await CreateContextAsync();
        return await context.Ingredients
            .OrderBy(ingredient => ingredient.Id)
            .Select(ingredient => ingredient.Id)
            .Take(count)
            .ToArrayAsync();
    }

    public async Task<int[]> GetDishIdsAsync(int count)
    {
        await using var context = await CreateContextAsync();
        return await context.Dishes
            .OrderBy(dish => dish.Id)
            .Select(dish => dish.Id)
            .Take(count)
            .ToArrayAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}
