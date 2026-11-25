using Mau.Core.Entities;

namespace Mau.Web.ViewModels.Admin;

/// <summary>One editable synonym row.</summary>
public class SynonymFormRow
{
    public string? Name { get; set; }

    public SynonymLanguage Language { get; set; } = SynonymLanguage.Filipino;
}
