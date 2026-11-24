namespace Mau.Core.Entities;

/// <summary>A dish a user saved as a favorite.</summary>
public class Favorite
{
    public string UserId { get; set; } = "";
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public DateTime AddedAt { get; set; }
}
