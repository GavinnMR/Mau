using Mau.Core.Entities;

namespace Mau.Core.Users;

/// <summary>What a user prefers (FR-08). Every part is optional.</summary>
public sealed record UserPreferences(
    int? PreferredProteinId, CookingMethod? PreferredMethod, int? MaxCookTimeMinutes)
{
    public const int MinCookTime = 5;
    public const int MaxCookTime = 600;

    public static UserPreferences None { get; } = new(null, null, null);

    public bool IsEmpty => this == None;
}
