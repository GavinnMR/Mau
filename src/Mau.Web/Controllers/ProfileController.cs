using System.Security.Claims;
using Mau.Core.Catalog;
using Mau.Core.Users;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

/// <summary>The signed-in user's pantry.</summary>
[Authorize]
public sealed class ProfileController : Controller
{
    private readonly CatalogStore _catalog;
    private readonly IPantryService _pantryService;

    public ProfileController(CatalogStore catalog, IPantryService pantryService)
    {
        _catalog = catalog;
        _pantryService = pantryService;
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
}
