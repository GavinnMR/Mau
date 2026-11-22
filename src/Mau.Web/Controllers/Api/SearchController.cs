using Mau.Core.Search;
using Mau.Web.Api;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Route("api/search")]
public sealed class SearchController : ApiControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpPost]
    public IActionResult Search([FromBody] SearchRequestDto request)
    {
        var result = _searchService.Search(new SearchRequest(request.Text, request.ExcludedIds));
        return Ok(DishMapper.ToDto(result));
    }
}
