using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Hamco.Services;

namespace Hamco.Api.Controllers;

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

    [HttpGet("register")]
    public IActionResult Register()
    {
        ViewBag.AllowRegistration = _configuration.GetValue<bool>("ALLOW_REGISTRATION", false);
        return View();
    }

    [HttpGet("verify-email")]
    public IActionResult VerifyEmail([FromQuery] string? token = null)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            return Redirect($"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet("verification-pending")]
    public IActionResult VerificationPending([FromQuery] string? email = null)
    {
        ViewBag.Email = email;
        return View();
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword([FromQuery] string token)
    {
        ViewBag.Token = token;
        return View();
    }
}
