using System.Security.Claims;
using Mau.Core.Feedback;
using Mau.Web.Api;
using Mau.Web.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Authorize]
[Route("api/feedback")]
public sealed class FeedbackApiController : ApiControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly SearchTokens _searchTokens;

    public FeedbackApiController(IFeedbackService feedbackService, SearchTokens searchTokens)
    {
        _feedbackService = feedbackService;
        _searchTokens = searchTokens;
    }

    [HttpPost]
    public async Task<IActionResult> Give([FromBody] FeedbackRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var action = request.Type switch
        {
            "cooked" => FeedbackAction.Cooked,
            "thumbsUp" => FeedbackAction.ThumbsUp,
            _ => FeedbackAction.ThumbsDown,
        };

        var result = await _feedbackService.GiveAsync(
            userId, request.DishId, action, _searchTokens.ReadKey(request.Search));

        return result.Outcome switch
        {
            FeedbackOutcome.Saved => Ok(ToDto(request.DishId, result)),
            FeedbackOutcome.DishNotFound =>
                NotFound(new ApiErrorResponse(new[] { "Dish not found." })),
            FeedbackOutcome.SearchRequired => BadRequest(new ApiErrorResponse(
                new[] { "Open this dish from your search results to mark it as cooked." })),
            _ => Conflict(new ApiErrorResponse(
                new[] { "You already marked this dish as cooked for this search." })),
        };
    }

    internal static FeedbackResponseDto ToDto(int dishId, FeedbackResult result) => new(
        dishId,
        result.User.CookedCount,
        result.User.Vote switch { Vote.Up => "up", Vote.Down => "down", _ => "none" },
        result.Totals.CookedCount,
        result.Totals.UpCount,
        result.Totals.DownCount);
}
