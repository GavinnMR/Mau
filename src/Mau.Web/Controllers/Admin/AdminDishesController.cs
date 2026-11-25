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

    [HttpGet("Create")]
    public IActionResult Create()
    {
        var form = new DishFormViewModel();
        AdminMapper.PadDishRows(form, 8);
        form.IngredientOptions = AdminMapper.IngredientOptions(_catalog.Current);
        return View("Form", form);
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(DishFormViewModel form)
    {
        var result = await _adminService.CreateDishAsync(AdminMapper.ToDishInput(form));

        if (!result.Succeeded)
        {
            AdminMapper.AddErrors(ModelState, result);
            AdminMapper.PadDishRows(form, 3);
            form.IngredientOptions = AdminMapper.IngredientOptions(_catalog.Current);
            return View("Form", form);
        }

        TempData["StatusMessage"] = "Dish saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public IActionResult Edit(int id)
    {
        var dish = _catalog.Current.DishesById.GetValueOrDefault(id);
        if (dish is null)
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        var form = AdminMapper.FromDish(dish);
        form.IngredientOptions = AdminMapper.IngredientOptions(_catalog.Current);
        return View("Form", form);
    }

    [HttpPost("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, DishFormViewModel form)
    {
        if (!_catalog.Current.DishesById.ContainsKey(id))
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        form.Id = id;
        var result = await _adminService.UpdateDishAsync(
            id, AdminMapper.ToDishInput(form));

        if (!result.Succeeded)
        {
            AdminMapper.AddErrors(ModelState, result);
            AdminMapper.PadDishRows(form, 3);
            form.IngredientOptions = AdminMapper.IngredientOptions(_catalog.Current);
            return View("Form", form);
        }

        TempData["StatusMessage"] = "Dish saved.";
        return RedirectToAction(nameof(Index));
    }

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
