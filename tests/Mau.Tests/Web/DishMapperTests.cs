using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Web.Mapping;

namespace Mau.Tests.Web;

public class DishMapperTests
{
    [Fact]
    public void ToViewModel_QuantityOneKilogram_FormatsAsOneKg()
    {
        AssertAmount(1m, "kg", "1 kg");
    }

    [Fact]
    public void ToViewModel_HalfCup_FormatsAsZeroPointFiveCup()
    {
        AssertAmount(0.5m, "cup", "0.5 cup");
    }

    [Fact]
    public void ToViewModel_QuantityWithoutUnit_FormatsNumberOnly()
    {
        AssertAmount(2m, null, "2");
    }

    [Fact]
    public void ToViewModel_NullQuantity_FormatsToTaste()
    {
        AssertAmount(null, null, "to taste");
    }

    private static void AssertAmount(decimal? quantity, string? unit, string expected)
    {
        var dish = new CatalogDish(
            1,
            "Test dish",
            "Description",
            CookingMethod.Fried,
            10,
            Array.Empty<string>(),
            new[]
            {
                new CatalogDishIngredient(1, "ingredient", quantity, unit, false, false),
            });

        var model = DishMapper.ToViewModel(dish);

        Assert.Equal(expected, model.Ingredients[0].AmountText);
    }
}
