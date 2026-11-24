using System.Security.Claims;
using Mau.Core.Users;
using Mau.Web.Api;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Route("api/search-log")]
public sealed class SearchLogController : ApiControllerBase
{
    private readonly ISearchHistoryService _historyService;

    public SearchLogController(ISearchHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] SearchRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var logged = await _historyService.RecordAsync(
            userId, request.Text, request.ExcludedIds);

        return Ok(new SearchLogResponseDto(logged));
    }
}
