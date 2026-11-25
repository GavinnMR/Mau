using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Web.Mapping;
using Mau.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Admin;

[Route("Admin/Dishes")]
public sealed class AdminDishesController : AdminControllerBase
{
    private readonly ICatalogAdminService _adminService;
    private readonly CatalogStore _catalog;

    public AdminDishesController(
        ICatalogAdminService adminService, CatalogStore catalog)
    {
        _adminService = adminService;
        _catalog = catalog;
    }

    [HttpGet("")]
    public IActionResult Index(string? q) =>
        View(AdminMapper.ToDishListViewModel(_catalog.Current, q));

    [HttpGet("Delete/{id:int}")]
    public IActionResult Delete(int id)
    {
        var dish = _catalog.Current.DishesById.GetValueOrDefault(id);
        if (dish is null)
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        return View(new DishDeleteViewModel(dish.Id, dish.Name));
    }

    [HttpPost("Delete/{id:int}")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!_catalog.Current.DishesById.ContainsKey(id))
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        var result = await _adminService.DeleteDishAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = "Dish deleted.";
        return RedirectToAction(nameof(Index));
    }
}
