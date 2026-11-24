using System.Security.Claims;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public sealed class DishController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IFavoriteService _favoriteService;

    public DishController(
        ISearchService searchService, IFavoriteService favoriteService)
    {
        _searchService = searchService;
        _favoriteService = favoriteService;
    }

    public async Task<IActionResult> Details(int id)
    {
        var dish = _searchService.GetDish(id);
        if (dish is null)
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = userId is not null;
        var isFavorite = isAuthenticated &&
            await _favoriteService.IsFavoriteAsync(userId!, id);

        return View(DishMapper.ToViewModel(dish, isAuthenticated, isFavorite));
    }
}
