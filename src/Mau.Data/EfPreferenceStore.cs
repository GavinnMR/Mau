using Mau.Core.Entities;
using Mau.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Mau.Data;

/// <summary>Stores a user's preferences in the database.</summary>
public sealed class EfPreferenceStore : IPreferenceStore
{
    private readonly IDbContextFactory<MauDbContext> _contextFactory;

    public EfPreferenceStore(IDbContextFactory<MauDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<UserPreferences> GetAsync(string userId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var row = await context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(preference => preference.UserId == userId, ct);

        return row is null
            ? UserPreferences.None
            : new UserPreferences(row.PreferredProteinId, row.PreferredMethod, row.MaxCookTimeMinutes);
    }

    public async Task SaveAsync(
        string userId, UserPreferences preferences, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var row = await context.UserPreferences
            .FirstOrDefaultAsync(preference => preference.UserId == userId, ct);

        if (preferences.IsEmpty)
        {
            if (row is not null)
            {
                context.UserPreferences.Remove(row);
                await context.SaveChangesAsync(ct);
            }

            return;
        }

        if (row is null)
        {
            row = new UserPreference { UserId = userId };
            context.UserPreferences.Add(row);
        }

        row.PreferredProteinId = preferences.PreferredProteinId;
        row.PreferredMethod = preferences.PreferredMethod;
        row.MaxCookTimeMinutes = preferences.MaxCookTimeMinutes;
        await context.SaveChangesAsync(ct);
    }
}
