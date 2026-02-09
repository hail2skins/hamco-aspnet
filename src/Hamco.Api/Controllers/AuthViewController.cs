using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Hamco.Services;

namespace Hamco.Api.Controllers;

/// <summary>
/// MVC controller for authentication UI views (Register, Login pages).
/// </summary>
/// <remarks>
/// This controller returns Razor views for authentication flows.
/// The actual authentication logic happens in the API AuthController.
/// 
/// Routes:
/// - GET /auth/register - Registration page
/// - GET /auth/login - Login page (optional, could redirect to API endpoint)
/// </remarks>
public class AuthViewController : BaseController
{
    private readonly IConfiguration _configuration;

    public AuthViewController(
        IConfiguration configuration,
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer) 
        : base(sloganRandomizer, imageRandomizer)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Displays the registration page.
    /// </summary>
    /// <returns>Registration view.</returns>
    /// <remarks>
    /// The view checks ALLOW_REGISTRATION environment variable and shows:
    /// - Registration form if registration is enabled
    /// - Disabled message if registration is blocked
    /// 
    /// Form submission is handled by JavaScript that calls /api/auth/register endpoint.
    /// </remarks>
    public IActionResult Register()
    {
        // Pass registration status to view
        ViewBag.AllowRegistration = _configuration.GetValue<bool>("ALLOW_REGISTRATION", false);
        return View();
    }

    /// <summary>
    /// Displays the login page.
    /// </summary>
    /// <returns>Login view or redirect to API login endpoint.</returns>
    /// <remarks>
    /// Currently redirects to API login endpoint.
    /// Future: Could show a Razor view with login form.
    /// </remarks>
    public IActionResult Login()
    {
        // For now, redirect to API login endpoint
        // Future: Could show a Razor view with login form
        return Redirect("/api/auth/login");
    }
}
