using System.Text.Json.Serialization;
using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Data;
using Mau.Web.Api;
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

builder.Services.AddDbContextFactory<MauDbContext>((serviceProvider, options) =>
    options.UseSqlite(
        serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

builder.Services.AddSingleton<ICatalogLoader, EfCatalogLoader>();
builder.Services.AddSingleton<CatalogStore>();
builder.Services.AddSingleton<ISearchService, SearchService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
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
