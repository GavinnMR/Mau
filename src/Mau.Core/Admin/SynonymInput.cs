using Mau.Core.Entities;

namespace Mau.Core.Admin;

/// <summary>One synonym row submitted from an admin form.</summary>
public sealed record SynonymInput(string? Name, SynonymLanguage Language);
