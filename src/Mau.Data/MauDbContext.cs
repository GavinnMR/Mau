using Mau.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>EF Core context for the Mau catalog.</summary>
public class MauDbContext : DbContext
{
    public MauDbContext(DbContextOptions<MauDbContext> options) : base(options)
    {
    }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientSynonym> IngredientSynonyms => Set<IngredientSynonym>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.Property(ingredient => ingredient.CanonicalName)
                .IsRequired()
                .HasMaxLength(100)
                .UseCollation("NOCASE");
            entity.HasIndex(ingredient => ingredient.CanonicalName).IsUnique();
            entity.Property(ingredient => ingredient.Category)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<IngredientSynonym>(entity =>
        {
            entity.Property(synonym => synonym.Name)
                .IsRequired()
                .HasMaxLength(100)
                .UseCollation("NOCASE");
            entity.HasIndex(synonym => synonym.Name).IsUnique();
            entity.Property(synonym => synonym.Language)
                .HasConversion<string>()
                .HasMaxLength(10);
            entity.HasOne(synonym => synonym.Ingredient)
                .WithMany(ingredient => ingredient.Synonyms)
                .HasForeignKey(synonym => synonym.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Dish>(entity =>
        {
            entity.Property(dish => dish.Name)
                .IsRequired()
                .HasMaxLength(120)
                .UseCollation("NOCASE");
            entity.HasIndex(dish => dish.Name).IsUnique();
            entity.Property(dish => dish.Description)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(dish => dish.CookingMethod)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(dish => dish.Steps)
                .IsRequired()
                .HasMaxLength(4000);
        });

        modelBuilder.Entity<DishIngredient>(entity =>
        {
            entity.HasKey(row => new { row.DishId, row.IngredientId });
            entity.HasOne(row => row.Dish)
                .WithMany(dish => dish.Ingredients)
                .HasForeignKey(row => row.DishId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(row => row.Ingredient)
                .WithMany()
                .HasForeignKey(row => row.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(row => row.Unit).HasMaxLength(20);
        });
    }
}
