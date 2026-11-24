namespace Mau.Core.Entities;

/// <summary>One recorded search.</summary>
public class SearchLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public DateTime SearchedAt { get; set; }
    public int ResultCount { get; set; }
    public List<SearchLogIngredient> Ingredients { get; set; } = new();
}
