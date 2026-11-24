using Mau.Core.Admin;
using Mau.Core.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Writes admin catalog changes to the database.</summary>
public sealed class EfCatalogAdminStore : ICatalogAdminStore
{
    private const string AddOrUpdateForeignKeyMessage =
        "A referenced item no longer exists.";

    private const string DeleteForeignKeyMessage =
        "It is still in use and cannot be deleted.";

    private readonly IDbContextFactory<MauDbContext> _contextFactory;

    public EfCatalogAdminStore(IDbContextFactory<MauDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<int> AddIngredientAsync(
        IngredientInput input, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ingredient = new Ingredient
        {
            CanonicalName = input.Name!,
            Category = input.Category,
            IsStaple = input.IsStaple,
            Synonyms = input.Synonyms
                .Select(synonym => new IngredientSynonym
                {
                    Name = synonym.Name!,
                    Language = synonym.Language,
                })
                .ToList(),
        };

        db.Ingredients.Add(ingredient);
        await ExecuteWithMappingAsync(
            () => db.SaveChangesAsync(ct), AddOrUpdateForeignKeyMessage);

        await tx.CommitAsync(ct);
        return ingredient.Id;
    }

    public async Task UpdateIngredientAsync(
        int id, IngredientInput input, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var ingredient = await db.Ingredients
            .SingleOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (ingredient is null)
        {
            throw new CatalogConflictException("Item not found.");
        }

        ingredient.CanonicalName = input.Name!;
        ingredient.Category = input.Category;
        ingredient.IsStaple = input.IsStaple;

        await ExecuteWithMappingAsync(
            () => db.IngredientSynonyms
                .Where(synonym => synonym.IngredientId == id)
                .ExecuteDeleteAsync(ct),
            AddOrUpdateForeignKeyMessage);

        foreach (var synonym in input.Synonyms)
        {
            db.IngredientSynonyms.Add(new IngredientSynonym
            {
                IngredientId = id,
                Name = synonym.Name!,
                Language = synonym.Language,
            });
        }

        await ExecuteWithMappingAsync(
            () => db.SaveChangesAsync(ct), AddOrUpdateForeignKeyMessage);

        await tx.CommitAsync(ct);
    }

    public async Task DeleteIngredientAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var deleted = await ExecuteWithMappingAsync(
            () => db.Ingredients
                .Where(ingredient => ingredient.Id == id)
                .ExecuteDeleteAsync(ct),
            DeleteForeignKeyMessage);

        if (deleted == 0)
        {
            throw new CatalogConflictException("Item not found.");
        }

        await tx.CommitAsync(ct);
    }

    public async Task<int> AddDishAsync(
        DishInput input, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var dish = new Dish
        {
            Name = input.Name!,
            Description = input.Description!,
            CookingMethod = input.CookingMethod,
            CookTimeMinutes = input.CookTimeMinutes,
            Steps = string.Join("\n", input.Steps),
            Ingredients = input.Ingredients
                .Select((row, position) => new DishIngredient
                {
                    IngredientId = row.IngredientId!.Value,
                    Position = position,
                    Quantity = row.Quantity,
                    Unit = row.Unit,
                    IsOptional = row.IsOptional,
                })
                .ToList(),
        };

        db.Dishes.Add(dish);
        await ExecuteWithMappingAsync(
            () => db.SaveChangesAsync(ct), AddOrUpdateForeignKeyMessage);

        await tx.CommitAsync(ct);
        return dish.Id;
    }

    public async Task UpdateDishAsync(
        int id, DishInput input, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var dish = await db.Dishes
            .SingleOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (dish is null)
        {
            throw new CatalogConflictException("Item not found.");
        }

        dish.Name = input.Name!;
        dish.Description = input.Description!;
        dish.CookingMethod = input.CookingMethod;
        dish.CookTimeMinutes = input.CookTimeMinutes;
        dish.Steps = string.Join("\n", input.Steps);

        await ExecuteWithMappingAsync(
            () => db.DishIngredients
                .Where(row => row.DishId == id)
                .ExecuteDeleteAsync(ct),
            AddOrUpdateForeignKeyMessage);

        var position = 0;
        foreach (var row in input.Ingredients)
        {
            db.DishIngredients.Add(new DishIngredient
            {
                DishId = id,
                IngredientId = row.IngredientId!.Value,
                Position = position++,
                Quantity = row.Quantity,
                Unit = row.Unit,
                IsOptional = row.IsOptional,
            });
        }

        await ExecuteWithMappingAsync(
            () => db.SaveChangesAsync(ct), AddOrUpdateForeignKeyMessage);

        await tx.CommitAsync(ct);
    }

    public async Task DeleteDishAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var deleted = await ExecuteWithMappingAsync(
            () => db.Dishes
                .Where(dish => dish.Id == id)
                .ExecuteDeleteAsync(ct),
            DeleteForeignKeyMessage);

        if (deleted == 0)
        {
            throw new CatalogConflictException("Item not found.");
        }

        await tx.CommitAsync(ct);
    }

    private static async Task<int> ExecuteWithMappingAsync(
        Func<Task<int>> action, string foreignKeyMessage)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateException e) when (
            e.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite)
        {
            throw MapConflict(sqlite, foreignKeyMessage);
        }
        catch (SqliteException sqlite) when (sqlite.SqliteErrorCode == 19)
        {
            throw MapConflict(sqlite, foreignKeyMessage);
        }
    }

    private static CatalogConflictException MapConflict(
        SqliteException exception, string foreignKeyMessage)
    {
        if (exception.Message.Contains("UNIQUE", StringComparison.Ordinal))
        {
            return new CatalogConflictException("That name is already in use.");
        }

        return new CatalogConflictException(foreignKeyMessage);
    }
}
