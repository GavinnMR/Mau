using Mau.Core.Catalog;
using Mau.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Admin;

[Route("Admin")]
public sealed class AdminController : AdminControllerBase
{
    private readonly CatalogStore _catalog;

    public AdminController(CatalogStore catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var snapshot = _catalog.Current;

        return View(new AdminDashboardViewModel(
            snapshot.Dishes.Count,
            snapshot.Ingredients.Count,
            snapshot.Ingredients.Sum(ingredient => ingredient.Synonyms.Count)));
    }
}
