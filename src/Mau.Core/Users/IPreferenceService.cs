using Mau.Core.Admin;

namespace Mau.Core.Users;

/// <summary>Reads and saves a user's preferences (FR-08, UC8).</summary>
public interface IPreferenceService
{
    Task<UserPreferences> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Validates and saves; returns the errors, or an empty list when saved.</summary>
    Task<IReadOnlyList<ValidationError>> SaveAsync(
        string userId, UserPreferences preferences, CancellationToken ct = default);
}
