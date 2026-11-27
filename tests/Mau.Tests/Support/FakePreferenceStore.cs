using Mau.Core.Users;

namespace Mau.Tests.Support;

public sealed class FakePreferenceStore : IPreferenceStore
{
    private readonly Dictionary<string, UserPreferences> _rows = new();

    public int SaveCount { get; private set; }

    public Task<UserPreferences> GetAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(_rows.GetValueOrDefault(userId, UserPreferences.None));

    public Task SaveAsync(string userId, UserPreferences preferences, CancellationToken ct = default)
    {
        SaveCount++;
        if (preferences.IsEmpty)
        {
            _rows.Remove(userId);
        }
        else
        {
            _rows[userId] = preferences;
        }

        return Task.CompletedTask;
    }
}
