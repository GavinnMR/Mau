using System.ComponentModel.DataAnnotations;
using Mau.Core.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mau.Web.ViewModels;

/// <summary>The preferences form (FR-08).</summary>
public sealed class PreferencesViewModel
{
    [Display(Name = "Preferred protein")]
    public int? PreferredProteinId { get; set; }

    [Display(Name = "Preferred cooking method")]
    public CookingMethod? PreferredMethod { get; set; }

    [Display(Name = "Maximum cooking time (minutes)")]
    public int? MaxCookTimeMinutes { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> ProteinOptions { get; set; } = [];
}
