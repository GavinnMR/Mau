using Mau.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Data;

/// <summary>Creates and seeds the database at startup.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<MauDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        await context.Database.MigrateAsync();

        if (!await context.Ingredients.AnyAsync())
        {
            await CatalogSeeder.SeedAsync(context);
        }
    }
}
