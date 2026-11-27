using System.Diagnostics;
using System.Security.Claims;
using Mau.Core.Catalog;
using Mau.Core.Users;
using Mau.Web.Models;
using Mau.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public class HomeController : Controller
{
    private readonly IPantryService _pantryService;
    private readonly IPreferenceService _preferenceService;
    private readonly CatalogStore _catalog;

    public HomeController(
        IPantryService pantryService, IPreferenceService preferenceService, CatalogStore catalog)
    {
        _pantryService = pantryService;
        _preferenceService = preferenceService;
        _catalog = catalog;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return View(new SearchPageViewModel(false, 0));
        }

        var pantryCount = (await _pantryService.GetIdsAsync(userId)).Count;
        var preferences = await _preferenceService.GetAsync(userId);
        var summary = new[]
            {
                preferences.PreferredProteinId is int id
                    ? _catalog.Current.IngredientsById[id].CanonicalName
                    : null,
                preferences.PreferredMethod?.ToString(),
            }
            .Where(part => part is not null)
            .ToList();

        return View(new SearchPageViewModel(
            true,
            pantryCount,
            preferences.MaxCookTimeMinutes,
            summary.Count == 0 ? null : string.Join(", ", summary)));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
