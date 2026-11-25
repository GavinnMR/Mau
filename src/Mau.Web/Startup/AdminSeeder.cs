using Mau.Core.Users;
using Mau.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mau.Web.Startup;

/// <summary>Creates the Admin role and the first admin account from configuration.</summary>
public static class AdminSeeder
{
    public static async Task EnsureAdminAsync(
        IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Mau.AdminSeeder");

        if (!await roles.RoleExistsAsync(AppRoles.Admin))
        {
            await roles.CreateAsync(new IdentityRole(AppRoles.Admin));
        }

        var admins = await users.GetUsersInRoleAsync(AppRoles.Admin);
        if (admins.Count > 0)
        {
            return;
        }

        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "No admin exists and Admin:Email / Admin:Password are not set.");
            return;
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError(
                    "Could not create the admin account. Errors: {Codes}.",
                    string.Join(", ", created.Errors.Select(error => error.Code)));
                return;
            }
        }

        await users.AddToRoleAsync(user, AppRoles.Admin);
        logger.LogInformation("Admin account ready: {Email}", email);
    }
}
