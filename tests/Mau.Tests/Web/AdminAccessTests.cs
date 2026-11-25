using System.Net;
using Mau.Core.Catalog;
using Mau.Core.Users;
using Mau.Data.Identity;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class AdminAccessTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AdminAccessTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Startup_CreatesConfiguredAdminInRole()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(AuthHelper.AdminEmail);

        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user!, AppRoles.Admin));
    }

    [Fact]
    public async Task WithoutAdminConfiguration_OnlyCreatesRole()
    {
        using var factory = new MauWebFactoryWithoutAdmin();
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roles.RoleExistsAsync(AppRoles.Admin));
        Assert.Empty(await users.GetUsersInRoleAsync(AppRoles.Admin));
    }

    [Fact]
    public async Task Guest_GetAdmin_RedirectsToLogin()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task NormalUser_GetAdmin_RedirectsToAccessDenied()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Admin_GetAdmin_ShowsDashboardCounts()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.LoginAsAdminAsync(client);
        var catalog = _factory.Services.GetRequiredService<CatalogStore>().Current;

        using var response = await client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin", html);
        Assert.Contains($"Dishes: {catalog.Dishes.Count}", html);
        Assert.Contains($"Ingredients: {catalog.Ingredients.Count}", html);
        Assert.Contains(
            $"Synonyms: {catalog.Ingredients.Sum(ingredient => ingredient.Synonyms.Count)}",
            html);
    }

    [Fact]
    public async Task Navbar_ShowsAdminLinkOnlyForAdmins()
    {
        using var admin = AuthHelper.CreateClient(_factory);
        await AuthHelper.LoginAsAdminAsync(admin);
        using var normal = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(normal);
        using var guest = AuthHelper.CreateClient(_factory);

        using var adminPage = await admin.GetAsync("/");
        using var normalPage = await normal.GetAsync("/");
        using var guestPage = await guest.GetAsync("/");

        Assert.Contains("href=\"/Admin\"", await adminPage.Content.ReadAsStringAsync());
        Assert.DoesNotContain("href=\"/Admin\"", await normalPage.Content.ReadAsStringAsync());
        Assert.DoesNotContain("href=\"/Admin\"", await guestPage.Content.ReadAsStringAsync());
    }
}
