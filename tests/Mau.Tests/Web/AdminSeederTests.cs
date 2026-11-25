using Mau.Core.Users;
using Mau.Data;
using Mau.Data.Identity;
using Mau.Web.Startup;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Web;

public class AdminSeederTests
{
    [Fact]
    public async Task NoConfiguration_CreatesRoleWithoutUser()
    {
        await using var host = await AdminTestHost.CreateAsync();
        await AdminSeeder.EnsureAdminAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roles.RoleExistsAsync(AppRoles.Admin));
        Assert.Empty(await users.GetUsersInRoleAsync(AppRoles.Admin));
    }

    [Fact]
    public async Task ConfiguredEmailWithoutUser_CreatesAdminWithPassword()
    {
        const string email = "seeded-admin@example.com";
        const string password = "Adm1nPassw0rd";
        await using var host = await AdminTestHost.CreateAsync(email, password);

        await AdminSeeder.EnsureAdminAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await users.CheckPasswordAsync(user!, password));
        Assert.True(await users.IsInRoleAsync(user!, AppRoles.Admin));
    }

    [Fact]
    public async Task ExistingNormalUser_KeepsOldPasswordAndGetsRole()
    {
        const string email = "existing-admin@example.com";
        const string oldPassword = "OldPassw0rd1";
        const string configuredPassword = "Adm1nPassw0rd";
        await using var host = await AdminTestHost.CreateAsync(email, configuredPassword);

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await users.CreateAsync(
                new ApplicationUser { UserName = email, Email = email }, oldPassword);
            Assert.True(created.Succeeded);
        }

        await AdminSeeder.EnsureAdminAsync(host.Services);

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.True(await users.CheckPasswordAsync(user!, oldPassword));
            Assert.False(await users.CheckPasswordAsync(user!, configuredPassword));
            Assert.True(await users.IsInRoleAsync(user!, AppRoles.Admin));
        }
    }

    [Fact]
    public async Task ExistingAdmin_IgnoresDifferentConfiguredEmail()
    {
        const string firstEmail = "first-admin@example.com";
        const string firstPassword = "Adm1nPassw0rd";
        const string secondEmail = "second-admin@example.com";
        const string secondPassword = "Adm1nPassw0rd2";
        await using var host = await AdminTestHost.CreateAsync(firstEmail, firstPassword);
        await AdminSeeder.EnsureAdminAsync(host.Services);

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        configuration["Admin:Email"] = secondEmail;
        configuration["Admin:Password"] = secondPassword;
        await AdminSeeder.EnsureAdminAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await users.FindByEmailAsync(firstEmail));
        Assert.Null(await users.FindByEmailAsync(secondEmail));
        Assert.Single(await users.GetUsersInRoleAsync(AppRoles.Admin));
    }

    [Fact]
    public async Task WeakConfiguredPassword_CreatesNoUserAndDoesNotThrow()
    {
        const string email = "weak-admin@example.com";
        await using var host = await AdminTestHost.CreateAsync(email, "short");

        await AdminSeeder.EnsureAdminAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await roles.RoleExistsAsync(AppRoles.Admin));
        Assert.Null(await users.FindByEmailAsync(email));
        Assert.Empty(await users.GetUsersInRoleAsync(AppRoles.Admin));
    }

    [Fact]
    public async Task CalledTwice_LeavesExactlyOneAdmin()
    {
        const string email = "twice-admin@example.com";
        await using var host = await AdminTestHost.CreateAsync(email, "Adm1nPassw0rd");

        await AdminSeeder.EnsureAdminAsync(host.Services);
        await AdminSeeder.EnsureAdminAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Single(await users.GetUsersInRoleAsync(AppRoles.Admin));
    }

    private sealed class AdminTestHost : IAsyncDisposable
    {
        private readonly string _path;
        private readonly ServiceProvider _provider;

        private AdminTestHost(string path, ServiceProvider provider)
        {
            _path = path;
            _provider = provider;
        }

        public IServiceProvider Services => _provider;

        public static async Task<AdminTestHost> CreateAsync(
            string? email = null, string? password = null)
        {
            var path = Path.Combine(
                Path.GetTempPath(), $"mau-admin-{Guid.NewGuid():N}.db");

            var settings = new Dictionary<string, string?>();
            if (email is not null)
            {
                settings["Admin:Email"] = email;
            }

            if (password is not null)
            {
                settings["Admin:Password"] = password;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContextFactory<MauDbContext>(options =>
                options.UseSqlite($"Data Source={path}"));
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<MauDbContext>();
            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            });

            var provider = services.BuildServiceProvider();
            await DatabaseInitializer.InitializeAsync(provider);
            return new AdminTestHost(path, provider);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
