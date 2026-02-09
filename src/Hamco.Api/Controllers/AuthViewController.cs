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
/// </remarks>
[Route("auth")]
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
    [HttpGet("register")]
    public IActionResult Register()
    {
        // Pass registration status to view
        ViewBag.AllowRegistration = _configuration.GetValue<bool>("ALLOW_REGISTRATION", false);
        return View();
    }

    /// <summary>
    /// Displays the login page.
    /// </summary>
    /// <returns>Login view.</returns>
    /// <remarks>
    /// The view provides a login form that submits to /api/auth/login via JavaScript.
    /// Includes a placeholder "Forgot password?" link for future implementation.
    /// </remarks>
    [HttpGet("login")]
    public IActionResult Login()
    {
        return View();
    }
}
