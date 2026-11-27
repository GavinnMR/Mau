namespace Mau.Core.Entities;

/// <summary>A user's ranking preferences; one row per user, every field optional.</summary>
public class UserPreference
{
    public string UserId { get; set; } = "";
    public int? PreferredProteinId { get; set; }
    public Ingredient? PreferredProtein { get; set; }
    public CookingMethod? PreferredMethod { get; set; }
    public int? MaxCookTimeMinutes { get; set; }
}
