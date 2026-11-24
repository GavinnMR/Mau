using System.Security.Claims;
using Mau.Core.Catalog;
using Mau.Core.Users;
using Mau.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Authorize]
[Route("api/pantry")]
public sealed class PantryApiController : ApiControllerBase
{
    private readonly IPantryService _pantryService;
    private readonly CatalogStore _catalog;

    public PantryApiController(IPantryService pantryService, CatalogStore catalog)
    {
        _pantryService = pantryService;
        _catalog = catalog;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ids = await _pantryService.GetIdsAsync(userId);

        var ingredients = _catalog.Current.Ingredients
            .Where(ingredient => ids.Contains(ingredient.Id))
            .OrderBy(ingredient => ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(ingredient => new PantryIngredientDto(
                ingredient.Id, ingredient.CanonicalName))
            .ToList();

        return Ok(new PantryResponseDto(ingredients));
    }
}
