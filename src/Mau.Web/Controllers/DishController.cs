using System.Security.Claims;
using Mau.Core.Feedback;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Mapping;
using Mau.Web.Search;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public sealed class DishController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IFavoriteService _favoriteService;
    private readonly IFeedbackService _feedbackService;
    private readonly DishStatsCache _stats;
    private readonly SearchTokens _searchTokens;

    public DishController(
        ISearchService searchService, IFavoriteService favoriteService,
        IFeedbackService feedbackService, DishStatsCache stats, SearchTokens searchTokens)
    {
        _searchTokens = searchTokens;
        _searchService = searchService;
        _favoriteService = favoriteService;
        _feedbackService = feedbackService;
        _stats = stats;
    }

    public async Task<IActionResult> Details(int id, string? search)
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
        var searchKey = _searchTokens.ReadKey(search);
        var cookedState = searchKey is null ? "needs-search"
            : isAuthenticated && await _feedbackService.HasCookedForSearchAsync(userId!, id, searchKey)
                ? "done"
                : "available";
        var feedback = DishMapper.ToFeedbackViewModel(
            _stats.Get(id), userFeedback, cookedState, searchKey is null ? null : search);

        return View(DishMapper.ToViewModel(dish, isAuthenticated, isFavorite, feedback));
    }
}
