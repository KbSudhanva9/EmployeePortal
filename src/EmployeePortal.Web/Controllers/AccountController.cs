using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmployeePortal.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string? username, string? password, bool rememberMe, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            TempData["Error"] = "Username and password are required";
            return View();
        }

        try
        {
            var result = await _signInManager.PasswordSignInAsync(
                username, password, rememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User '{Username}' logged in successfully", username);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account '{Username}' locked out", username);
                TempData["Error"] = "Account locked due to too many failed login attempts. Please try again later.";
                return View();
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, rememberMe });
            }

            _logger.LogWarning("Failed login attempt for user '{Username}'", username);
            TempData["Error"] = "Invalid username or password";
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            TempData["Error"] = "An error occurred during login";
            return View();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public IActionResult Manage()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Dashboard.DashboardController.Index), "Dashboard");
    }

    public IActionResult LoginWith2fa(bool rememberMe, string? returnUrl = null)
    {
        // TODO: Implement 2FA
        return RedirectToAction(nameof(Login));
    }
}
