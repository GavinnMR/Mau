using System.Security.Claims;
using Mau.Core.Feedback;
using Mau.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers.Api;

[Authorize]
[Route("api/feedback")]
public sealed class FeedbackApiController : ApiControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackApiController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
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

        var result = await _feedbackService.GiveAsync(userId, request.DishId, action);
        if (result is null)
        {
            return NotFound(new ApiErrorResponse(new[] { "Dish not found." }));
        }

        return Ok(ToDto(request.DishId, result));
    }

    internal static FeedbackResponseDto ToDto(int dishId, FeedbackResult result) => new(
        dishId,
        result.User.CookedCount,
        result.User.Vote switch { Vote.Up => "up", Vote.Down => "down", _ => "none" },
        result.Totals.CookedCount,
        result.Totals.UpCount,
        result.Totals.DownCount);
}
