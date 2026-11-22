using Mau.Core.Search;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public sealed class DishController : Controller
{
    private readonly ISearchService _searchService;

    public DishController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public IActionResult Details(int id)
    {
        var dish = _searchService.GetDish(id);
        if (dish is null)
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }

        return View(DishMapper.ToViewModel(dish));
    }
}
