using System.Security.Claims;
using Mau.Core.Users;
using Mau.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Authorize]
[Route("api/favorites")]
public sealed class FavoritesApiController : ApiControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesApiController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpPut("{dishId:int}")]
    public async Task<IActionResult> Add(int dishId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var added = await _favoriteService.AddAsync(userId, dishId);

        if (!added)
        {
            return NotFound(new ApiErrorResponse(new[] { "Dish not found." }));
        }

        return Ok(new { status = "ok" });
    }

    [HttpDelete("{dishId:int}")]
    public async Task<IActionResult> Remove(int dishId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var removed = await _favoriteService.RemoveAsync(userId, dishId);

        if (!removed)
        {
            return NotFound(new ApiErrorResponse(new[] { "Dish not found." }));
        }

        return Ok(new { status = "ok" });
    }
}
