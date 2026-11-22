using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Mau.Web.Api;

/// <summary>Turns unhandled API exceptions into the standard error response.</summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled API exception.");

        context.Result = new ObjectResult(
            new ApiErrorResponse(new[] { "Something went wrong. Please try again." }))
        {
            StatusCode = 500,
        };
        context.ExceptionHandled = true;
    }
}
