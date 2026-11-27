namespace Mau.Core.Entities;

/// <summary>Feedback totals for one dish, kept in step with the feedback rows.</summary>
public class DishStat
{
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public int CookedCount { get; set; }
    public int UpCount { get; set; }
    public int DownCount { get; set; }
}
