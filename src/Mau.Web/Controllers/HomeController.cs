using System.Diagnostics;
using System.Security.Claims;
using Mau.Core.Users;
using Mau.Web.Models;
using Mau.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public class HomeController : Controller
{
    private readonly IPantryService _pantryService;

    public HomeController(IPantryService pantryService)
    {
        _pantryService = pantryService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var pantryCount = 0;

        if (userId is not null)
        {
            pantryCount = (await _pantryService.GetIdsAsync(userId)).Count;
        }

        return View(new SearchPageViewModel(userId is not null, pantryCount));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
