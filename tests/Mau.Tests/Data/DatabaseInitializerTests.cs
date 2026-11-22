using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Core.Search;
using Mau.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Data;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_EmptyDatabase_SeedsThirtyNineIngredientsAndFourteenDishes()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();

        Assert.Equal(39, await context.Ingredients.CountAsync());
        Assert.Equal(14, await context.Dishes.CountAsync());
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotDuplicateRows()
    {
        await using var database = await TempDatabase.CreateAsync();

        await DatabaseInitializer.InitializeAsync(database.Services);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(39, await context.Ingredients.CountAsync());
        Assert.Equal(14, await context.Dishes.CountAsync());
    }

    [Fact]
    public async Task EfCatalogLoader_LoadAsync_ReturnsSeededSnapshot()
    {
        await using var database = await TempDatabase.CreateAsync();

        var snapshot = await database.CreateLoader().LoadAsync();

        Assert.Equal(14, snapshot.Dishes.Count);
        var soySauce = snapshot.Ingredients.Single(i => i.CanonicalName == "soy sauce");
        Assert.Equal(soySauce.Id, snapshot.Recognizer.Parse("toyo,").Tokens[0].IngredientId);

        var adobo = snapshot.Dishes.Single(d => d.Name == "Chicken adobo");
        Assert.Equal("chicken", adobo.Ingredients[0].Name);
        Assert.Equal(4, adobo.Steps.Count);
    }

    [Fact]
    public async Task SearchService_OnLoadedSnapshot_ReturnsGarlicFriedChickenFirst()
    {
        await using var database = await TempDatabase.CreateAsync();
        var store = new CatalogStore(database.CreateLoader());
        await store.ReloadAsync();
        var service = new SearchService(store);

        var result = service.Search(new SearchRequest("chicken, garlic,", null));

        Assert.Equal("Garlic fried chicken", result.Dishes[0].Name);
    }

    [Fact]
    public async Task DeletingIngredientUsedByDish_ThrowsDbUpdateException()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();
        var chicken = await context.Ingredients.SingleAsync(i => i.CanonicalName == "chicken");

        context.Ingredients.Remove(chicken);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task AddingIngredientWithDifferentCaseDuplicate_ThrowsDbUpdateException()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();
        context.Ingredients.Add(new Ingredient
        {
            CanonicalName = "CHICKEN",
            Category = IngredientCategory.Protein,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

internal sealed class TempDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private TempDatabase(string path, ServiceProvider provider)
    {
        Path = path;
        _provider = provider;
    }

    public string Path { get; }

    public IServiceProvider Services => _provider;

    public static async Task<TempDatabase> CreateAsync()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"mau-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContextFactory<MauDbContext>(options =>
            options.UseSqlite($"Data Source={path}"));
        var provider = services.BuildServiceProvider();

        var database = new TempDatabase(path, provider);
        await DatabaseInitializer.InitializeAsync(provider);
        return database;
    }

    public Task<MauDbContext> CreateContextAsync() =>
        _provider.GetRequiredService<IDbContextFactory<MauDbContext>>().CreateDbContextAsync();

    public EfCatalogLoader CreateLoader() =>
        new(_provider.GetRequiredService<IDbContextFactory<MauDbContext>>());

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
