using Mau.Core.Search;
using Mau.Web.Api;
using Mau.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Route("api/hints")]
public sealed class HintsController : ApiControllerBase
{
    private readonly ISearchService _searchService;

    public HintsController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? prefix)
    {
        if (prefix is { Length: > 50 })
        {
            return BadRequest(new ApiErrorResponse(
                new[] { "Prefix must be at most 50 characters." }));
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Ok(new HintsResponseDto(Array.Empty<HintDto>()));
        }

        return Ok(new HintsResponseDto(DishMapper.ToDto(_searchService.GetHints(prefix))));
    }
}
