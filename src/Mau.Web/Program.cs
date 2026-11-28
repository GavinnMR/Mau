using System.Globalization;
using System.Text.Json.Serialization;
using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Engine;
using Mau.Core.Feedback;
using Mau.Core.Ranking;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Data;
using Mau.Data.Identity;
using Mau.Web.Api;
using Mau.Web.Search;
using Mau.Web.Startup;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews(options =>
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Invalid input."
                    : error.ErrorMessage)
                .ToList();
            return new BadRequestObjectResult(new ApiErrorResponse(errors));
        });

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<MauDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = ctx =>
        ApiAwareAuthEvents.Handle(ctx, StatusCodes.Status401Unauthorized, "Please log in.");
    options.Events.OnRedirectToAccessDenied = ctx =>
        ApiAwareAuthEvents.Handle(
            ctx,
            StatusCodes.Status403Forbidden,
            "You do not have permission to do that.");
});

builder.Services.AddDbContextFactory<MauDbContext>((serviceProvider, options) =>
    options.UseSqlite(
        serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

builder.Services.AddSingleton<ICatalogLoader, EfCatalogLoader>();
builder.Services.AddSingleton<CatalogStore>();
builder.Services.Configure<FuzzyOptions>(
    builder.Configuration.GetSection(FuzzyOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<FuzzyOptions>>().Value);
builder.Services.AddSingleton<ISearchService>(serviceProvider => new SearchService(
    serviceProvider.GetRequiredService<CatalogStore>(),
    serviceProvider.GetRequiredService<IRankingService>(),
    serviceProvider.GetRequiredService<FuzzyOptions>()));
builder.Services.Configure<RankingOptions>(
    builder.Configuration.GetSection(RankingOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<RankingOptions>>().Value);
builder.Services.AddSingleton<IRankingService>(serviceProvider => new RankingService(
    serviceProvider.GetRequiredService<DishStatsCache>(),
    serviceProvider.GetRequiredService<RankingOptions>()));

builder.Services.AddSingleton<ICatalogAdminStore, EfCatalogAdminStore>();
builder.Services.AddSingleton<ICatalogAdminService, CatalogAdminService>();

builder.Services.AddSingleton<IPantryStore, EfPantryStore>();
builder.Services.AddSingleton<IFavoriteStore, EfFavoriteStore>();
builder.Services.AddSingleton<ISearchLogStore, EfSearchLogStore>();
builder.Services.AddSingleton<IPantryService, PantryService>();
builder.Services.AddSingleton<IFavoriteService, FavoriteService>();
builder.Services.AddSingleton<ISearchHistoryService, SearchHistoryService>();
builder.Services.AddSingleton<IPreferenceStore, EfPreferenceStore>();
builder.Services.AddSingleton<INeverListStore, EfNeverListStore>();
builder.Services.AddSingleton<IPreferenceService, PreferenceService>();

builder.Services.AddSingleton<IFeedbackStore, EfFeedbackStore>();
builder.Services.AddSingleton<DishStatsCache>();
builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
builder.Services.AddSingleton<SearchTokens>();

var app = builder.Build();

var enUs = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(enUs),
    SupportedCultures = [enUs],
    SupportedUICultures = [enUs],
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DatabaseInitializer.InitializeAsync(app.Services);
await AdminSeeder.EnsureAdminAsync(app.Services);
await app.Services.GetRequiredService<CatalogStore>().ReloadAsync();
await app.Services.GetRequiredService<DishStatsCache>().ReloadAsync();

app.Run();

public partial class Program { }
