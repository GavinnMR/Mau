using System.Security.Claims;
using Mau.Core.Search;
using Mau.Core.Users;
using Mau.Web.Api;
using Mau.Web.Mapping;
using Mau.Web.Search;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Route("api/search")]
public sealed class SearchController : ApiControllerBase
{
    private readonly ISearchService _searchService;
    private readonly IPantryService _pantryService;
    private readonly SearchTokens _searchTokens;

    public SearchController(
        ISearchService searchService, IPantryService pantryService, SearchTokens searchTokens)
    {
        _searchService = searchService;
        _pantryService = pantryService;
        _searchTokens = searchTokens;
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        IReadOnlySet<int>? pantryIds = null;

        if (request.UsePantry && userId is not null)
        {
            pantryIds = await _pantryService.GetIdsAsync(userId);
        }

        var result = _searchService.Search(
            new SearchRequest(request.Text, request.ExcludedIds, pantryIds));

        var searchToken = result.Dishes.Count > 0 ? _searchTokens.Issue() : null;
        return Ok(DishMapper.ToDto(result, pantryIds is { Count: > 0 }, searchToken));
    }
}
