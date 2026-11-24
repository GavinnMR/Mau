using System.Text.Json.Serialization;
using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Data;
using Mau.Data.Identity;
using Mau.Web.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddSingleton<ISearchService, SearchService>();

builder.Services.AddSingleton<ICatalogAdminStore, EfCatalogAdminStore>();
builder.Services.AddSingleton<ICatalogAdminService, CatalogAdminService>();

builder.Services.AddSingleton<IPantryStore, EfPantryStore>();
builder.Services.AddSingleton<IFavoriteStore, EfFavoriteStore>();
builder.Services.AddSingleton<ISearchLogStore, EfSearchLogStore>();
builder.Services.AddSingleton<IPantryService, PantryService>();
builder.Services.AddSingleton<IFavoriteService, FavoriteService>();
builder.Services.AddSingleton<ISearchHistoryService, SearchHistoryService>();

var app = builder.Build();

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
await app.Services.GetRequiredService<CatalogStore>().ReloadAsync();

app.Run();

public partial class Program { }
