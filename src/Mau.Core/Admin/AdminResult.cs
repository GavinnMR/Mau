namespace Mau.Core.Admin;

/// <summary>The outcome of an admin catalog operation.</summary>
public sealed record AdminResult(
    bool Succeeded, IReadOnlyList<ValidationError> Errors, int? Id = null)
{
    public static AdminResult Ok(int? id = null) => new(true, [], id);

    public static AdminResult Fail(params ValidationError[] errors) =>
        new(false, errors);
}
