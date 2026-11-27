namespace Mau.Core.Entities;

/// <summary>One feedback event: a user cooked a dish or voted on it.</summary>
public class DishFeedback
{
    public int Id { get; set; }

    /// <summary>The user who gave the feedback; null once that account is deleted.</summary>
    public string? UserId { get; set; }

    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public FeedbackType Type { get; set; }

    /// <summary>For a cooked event, the search it came from; a user cooks a dish once per search.</summary>
    public string? SearchKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
