namespace Mau.Web.Api;

/// <summary>The error shape returned by every failing JSON endpoint.</summary>
public sealed record ApiErrorResponse(IReadOnlyList<string> Errors)
{
    public string Status => "error";
}
