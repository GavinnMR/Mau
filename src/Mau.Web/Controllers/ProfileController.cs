using System.Security.Claims;
using Mau.Core.Catalog;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

/// <summary>The signed-in user's pantry and favorites.</summary>
[Authorize]
public sealed class ProfileController : Controller
{
    private readonly CatalogStore _catalog;
    private readonly IPantryService _pantryService;
    private readonly ISearchService _searchService;
    private readonly IFavoriteService _favoriteService;
    private readonly ISearchHistoryService _historyService;

    public ProfileController(
        CatalogStore catalog,
        IPantryService pantryService,
        ISearchService searchService,
        IFavoriteService favoriteService,
        ISearchHistoryService historyService)
    {
        _catalog = catalog;
        _pantryService = pantryService;
        _searchService = searchService;
        _favoriteService = favoriteService;
        _historyService = historyService;
    }

    [HttpGet]
    public async Task<IActionResult> Pantry()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var selectedIds = await _pantryService.GetIdsAsync(userId);

        return View(ProfileMapper.ToPantryViewModel(_catalog.Current, selectedIds));
    }

    [HttpPost]
    public async Task<IActionResult> Pantry(List<int>? selectedIds)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var saved = await _pantryService.SaveAsync(userId, selectedIds);

        TempData["StatusMessage"] = saved == 1
            ? "Pantry saved: 1 ingredient."
            : $"Pantry saved: {saved} ingredients.";

        return RedirectToAction(nameof(Pantry));
    }

    [HttpGet]
    public async Task<IActionResult> Favorites()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var dishIds = await _favoriteService.GetDishIdsAsync(userId);

        return View(ProfileMapper.ToFavoritesViewModel(dishIds, _searchService));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveFavorite(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favoriteService.RemoveAsync(userId, id);

        TempData["StatusMessage"] = "Removed from favorites.";

        return RedirectToAction(nameof(Favorites));
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var entries = await _historyService.GetRecentAsync(userId);

        return View(ProfileMapper.ToHistoryViewModel(entries, _catalog.Current));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteHistory(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _historyService.DeleteAsync(userId, id);

        TempData["StatusMessage"] = "Search removed.";

        return RedirectToAction(nameof(History));
    }

    [HttpPost]
    public async Task<IActionResult> ClearHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _historyService.ClearAsync(userId);

        TempData["StatusMessage"] = "History cleared.";

        return RedirectToAction(nameof(History));
    }
}
