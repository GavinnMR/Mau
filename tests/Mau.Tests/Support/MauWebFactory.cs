using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Mau.Tests.Support;

public class MauWebFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mau-web-{Guid.NewGuid():N}.db");

    protected virtual string? AdminEmail => AuthHelper.AdminEmail;

    protected virtual string? AdminPassword => AuthHelper.AdminPassword;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
            };

            if (AdminEmail is not null)
            {
                settings["Admin:Email"] = AdminEmail;
            }

            if (AdminPassword is not null)
            {
                settings["Admin:Password"] = AdminPassword;
            }

            configuration.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
