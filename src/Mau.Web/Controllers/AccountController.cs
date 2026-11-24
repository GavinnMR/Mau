using Mau.Data.Identity;
using Mau.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mau.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                ModelState.AddModelError(
                    nameof(model.Email), "That email is already registered.");
            }

            foreach (var error in result.Errors.Where(error =>
                error.Code is not ("DuplicateUserName" or "DuplicateEmail")))
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["StatusMessage"] = "Welcome to Mau!";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl!)
                : RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Too many failed attempts. Try again in a few minutes."
                : "Invalid email or password.");
        ModelState.Remove(nameof(model.Password));
        model.Password = "";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["StatusMessage"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
