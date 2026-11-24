namespace Mau.Core.Admin;

/// <summary>One validation error; an empty field means it is not tied to one field.</summary>
public sealed record ValidationError(string Field, string Message);
