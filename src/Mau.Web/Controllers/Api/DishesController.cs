using Mau.Core.Search;
using Mau.Web.Api;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Route("api/dishes")]
public sealed class DishesController : ApiControllerBase
{
    private readonly ISearchService _searchService;

    public DishesController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var dish = _searchService.GetDish(id);
        if (dish is null)
        {
            return NotFound(new ApiErrorResponse(new[] { "Dish not found." }));
        }

        return Ok(DishMapper.ToDto(dish));
    }
}
