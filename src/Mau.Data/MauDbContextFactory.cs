using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mau.Data;

/// <summary>Creates the context for EF Core design-time commands.</summary>
public sealed class MauDbContextFactory : IDesignTimeDbContextFactory<MauDbContext>
{
    public MauDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MauDbContext>()
            .UseSqlite("Data Source=mau.db")
            .Options;

        return new MauDbContext(options);
    }
}
