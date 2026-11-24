using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Mau.Web.Api;

/// <summary>Returns JSON for API auth failures and redirects for pages.</summary>
internal static class ApiAwareAuthEvents
{
    public static Task Handle(
        RedirectContext<CookieAuthenticationOptions> ctx,
        int statusCode,
        string message)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = statusCode;
            return ctx.Response.WriteAsJsonAsync(new ApiErrorResponse([message]));
        }

        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }
}
