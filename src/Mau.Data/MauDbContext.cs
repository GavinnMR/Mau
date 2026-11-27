using Mau.Core.Entities;
using Mau.Data.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>EF Core context for the Mau catalog and Identity users.</summary>
public class MauDbContext : IdentityDbContext<ApplicationUser>
{
    public MauDbContext(DbContextOptions<MauDbContext> options) : base(options)
    {
    }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientSynonym> IngredientSynonyms => Set<IngredientSynonym>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<SearchLogIngredient> SearchLogIngredients => Set<SearchLogIngredient>();
    public DbSet<DishFeedback> DishFeedback => Set<DishFeedback>();
    public DbSet<DishStat> DishStats => Set<DishStat>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<NeverIngredient> NeverIngredients => Set<NeverIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            entity.HasOne(dish => dish.MainProtein)
                .WithMany()
                .HasForeignKey(dish => dish.MainProteinId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<PantryItem>(entity =>
        {
            entity.HasKey(item => new { item.UserId, item.IngredientId });
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Ingredient)
                .WithMany()
                .HasForeignKey(item => item.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(favorite => new { favorite.UserId, favorite.DishId });
            entity.Property(favorite => favorite.UserId).HasMaxLength(450);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(favorite => favorite.Dish)
                .WithMany()
                .HasForeignKey(favorite => favorite.DishId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SearchLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.UserId).HasMaxLength(450);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(log => new { log.UserId, log.SearchedAt });
        });

        modelBuilder.Entity<SearchLogIngredient>(entity =>
        {
            entity.HasKey(row => new { row.SearchLogId, row.IngredientId });
            entity.HasOne(row => row.SearchLog)
                .WithMany(log => log.Ingredients)
                .HasForeignKey(row => row.SearchLogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(row => row.Ingredient)
                .WithMany()
                .HasForeignKey(row => row.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DishFeedback>(entity =>
        {
            entity.HasKey(feedback => feedback.Id);
            entity.Property(feedback => feedback.UserId).HasMaxLength(450);
            entity.Property(feedback => feedback.Type)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(feedback => feedback.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(feedback => feedback.Dish)
                .WithMany()
                .HasForeignKey(feedback => feedback.DishId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(feedback => feedback.SearchKey).HasMaxLength(32);
            entity.HasIndex(feedback => new { feedback.UserId, feedback.DishId, feedback.SearchKey })
                .IsUnique()
                .HasFilter("\"SearchKey\" IS NOT NULL");
            entity.HasIndex(feedback => feedback.CreatedAt);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(preference => preference.UserId);
            entity.Property(preference => preference.UserId).HasMaxLength(450);
            entity.Property(preference => preference.PreferredMethod)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserPreference>(preference => preference.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(preference => preference.PreferredProtein)
                .WithMany()
                .HasForeignKey(preference => preference.PreferredProteinId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NeverIngredient>(entity =>
        {
            entity.HasKey(item => new { item.UserId, item.IngredientId });
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Ingredient)
                .WithMany()
                .HasForeignKey(item => item.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DishStat>(entity =>
        {
            entity.HasKey(stat => stat.DishId);
            entity.HasOne(stat => stat.Dish)
                .WithOne()
                .HasForeignKey<DishStat>(stat => stat.DishId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
