using Mau.Core.Admin;
using Mau.Core.Catalog;
using Mau.Web.Mapping;
using Mau.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Admin;

[Route("Admin/Ingredients")]
public sealed class AdminIngredientsController : AdminControllerBase
{
    private readonly ICatalogAdminService _adminService;
    private readonly CatalogStore _catalog;

    public AdminIngredientsController(
        ICatalogAdminService adminService, CatalogStore catalog)
    {
        _adminService = adminService;
        _catalog = catalog;
    }

    [HttpGet("")]
    public IActionResult Index(string? q) =>
        View(AdminMapper.ToIngredientListViewModel(_catalog.Current, q));

    [HttpGet("Create")]
    public IActionResult Create()
    {
        var form = new IngredientFormViewModel();
        AdminMapper.PadIngredientRows(form);
        return View("Form", form);
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(IngredientFormViewModel form)
    {
        var result = await _adminService.CreateIngredientAsync(
            AdminMapper.ToIngredientInput(form));

        if (!result.Succeeded)
        {
            AdminMapper.AddErrors(ModelState, result);
            AdminMapper.PadIngredientRows(form);
            return View("Form", form);
        }

        TempData["StatusMessage"] = "Ingredient saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public IActionResult Edit(int id)
    {
        var ingredient = _catalog.Current.IngredientsById.GetValueOrDefault(id);
        if (ingredient is null)
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        return View("Form", AdminMapper.FromIngredient(ingredient));
    }

    [HttpPost("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, IngredientFormViewModel form)
    {
        if (!_catalog.Current.IngredientsById.ContainsKey(id))
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        form.Id = id;
        var result = await _adminService.UpdateIngredientAsync(
            id, AdminMapper.ToIngredientInput(form));

        if (!result.Succeeded)
        {
            AdminMapper.AddErrors(ModelState, result);
            AdminMapper.PadIngredientRows(form);
            return View("Form", form);
        }

        TempData["StatusMessage"] = "Ingredient saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Delete/{id:int}")]
    public IActionResult Delete(int id)
    {
        var snapshot = _catalog.Current;
        var ingredient = snapshot.IngredientsById.GetValueOrDefault(id);
        if (ingredient is null)
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        var usedBy = snapshot.DishIdsByIngredientId.TryGetValue(id, out var dishIds)
            ? dishIds
                .Select(dishId => snapshot.DishesById[dishId].Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        return View(new IngredientDeleteViewModel(
            ingredient.Id, ingredient.CanonicalName, usedBy));
    }

    [HttpPost("Delete/{id:int}")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!_catalog.Current.IngredientsById.ContainsKey(id))
        {
            Response.StatusCode = 404;
            return View("AdminNotFound");
        }

        var result = await _adminService.DeleteIngredientAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = "Ingredient deleted.";
        return RedirectToAction(nameof(Index));
    }
}
