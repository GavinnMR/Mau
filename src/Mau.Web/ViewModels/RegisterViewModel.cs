using System.ComponentModel.DataAnnotations;

namespace Mau.Web.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be 8 to 100 characters.")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}
