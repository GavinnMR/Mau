namespace Mau.Core.Users;

/// <summary>Persistence for a user's preferences.</summary>
public interface IPreferenceStore
{
    Task<UserPreferences> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Saves the preferences; empty preferences remove the user's row.</summary>
    Task SaveAsync(string userId, UserPreferences preferences, CancellationToken ct = default);
}
