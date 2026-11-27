using System.Security.Claims;
using Mau.Core.Feedback;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public sealed class DishController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IFavoriteService _favoriteService;
    private readonly IFeedbackService _feedbackService;
    private readonly DishStatsCache _stats;

    public DishController(
        ISearchService searchService, IFavoriteService favoriteService,
        IFeedbackService feedbackService, DishStatsCache stats)
    {
        _searchService = searchService;
        _favoriteService = favoriteService;
        _feedbackService = feedbackService;
        _stats = stats;
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

        var userFeedback = isAuthenticated
            ? await _feedbackService.GetForUserAsync(userId!, id)
            : UserDishFeedback.None;
        var feedback = DishMapper.ToFeedbackViewModel(_stats.Get(id), userFeedback);

        return View(DishMapper.ToViewModel(dish, isAuthenticated, isFavorite, feedback));
    }
}
