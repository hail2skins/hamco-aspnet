using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Hamco.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HamcoDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ITransactionalEmailService _emailService;

    private const int TokenExpiryMinutes = 20;

    public AuthController(
        HamcoDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IConfiguration configuration,
        ITransactionalEmailService emailService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _configuration = configuration;
        _emailService = emailService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var allowRegistration = _configuration.GetValue<bool>("ALLOW_REGISTRATION", false);
        if (!allowRegistration)
        {
            return StatusCode(403, new { message = "Registration is currently disabled" });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser != null)
        {
            if (existingUser.IsEmailVerified)
            {
                return Conflict(new { message = "Email already exists" });
            }

            // Existing unverified account: resend verification token, no duplicate row.
            var resendToken = GenerateSecureToken();
            existingUser.EmailVerificationTokenHash = HashToken(resendToken);
            existingUser.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(TokenExpiryMinutes);
            await _context.SaveChangesAsync();

            await _emailService.SendVerificationEmailAsync(
                normalizedEmail,
                BuildAbsoluteUrl($"/auth/verify-email?token={Uri.EscapeDataString(resendToken)}"));

            return Ok(new
            {
                message = "Account already exists but is not verified. We sent a new verification email.",
                requiresEmailVerification = true,
                email = normalizedEmail
            });
        }

        var isFirstUser = !await _context.Users.AnyAsync();
        var verificationToken = GenerateSecureToken();

        var user = new User
        {
            Username = request.Username,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsAdmin = isFirstUser,
            IsEmailVerified = false,
            Roles = isFirstUser ? new List<string> { "Admin" } : new List<string>(),
            EmailVerificationTokenHash = HashToken(verificationToken),
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(TokenExpiryMinutes)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _emailService.SendVerificationEmailAsync(
            normalizedEmail,
            BuildAbsoluteUrl($"/auth/verify-email?token={Uri.EscapeDataString(verificationToken)}"));

        return Ok(new
        {
            message = "Registration successful. Please verify your email.",
            requiresEmailVerification = true,
            email = normalizedEmail
        });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Redirect("/auth/login?error=invalid_verification_token");
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.EmailVerificationTokenHash == tokenHash &&
            u.EmailVerificationTokenExpiresAt != null &&
            u.EmailVerificationTokenExpiresAt > now);

        if (user == null)
        {
            return Redirect("/auth/login?error=expired_or_invalid_verification_token");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;

        await _context.SaveChangesAsync();

        return Redirect("/auth/login?verified=1");
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (!user.IsEmailVerified)
        {
            return StatusCode(403, new { message = "Please verify your email before logging in." });
        }

        EnsureRolesPopulated(user);
        var token = _jwtService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Always return generic success to avoid email enumeration.
        if (user == null)
        {
            return Ok(new { message = "If your email is registered, you will receive a password reset link shortly." });
        }

        var resetToken = GenerateSecureToken();
        user.PasswordResetTokenHash = HashToken(resetToken);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(TokenExpiryMinutes);
        await _context.SaveChangesAsync();

        await _emailService.SendPasswordResetEmailAsync(
            normalizedEmail,
            BuildAbsoluteUrl($"/auth/reset-password?token={Uri.EscapeDataString(resetToken)}"));

        return Ok(new { message = "If your email is registered, you will receive a password reset link shortly." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var tokenHash = HashToken(request.Token);
        var now = DateTime.UtcNow;

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetTokenHash == tokenHash &&
            u.PasswordResetTokenExpiresAt != null &&
            u.PasswordResetTokenExpiresAt > now);

        if (user == null)
        {
            return BadRequest(new { message = "Invalid or expired reset token" });
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Password reset successful. You can now log in." });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(new { message = "Invalid token claims" });
        }

        var user = await _context.Users.FindAsync(userIdClaim);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        return Ok(new AuthResponse
        {
            Token = string.Empty,
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles,
            ExpiresAt = DateTime.MinValue
        });
    }

    [HttpPost("cookie")]
    public IActionResult SetAuthCookie([FromBody] SetCookieRequest request)
    {
        if (string.IsNullOrEmpty(request.Token))
        {
            return BadRequest(new { message = "Token is required" });
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_configuration.GetValue<bool>("DEVELOPMENT_MODE", false),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1),
            Path = "/"
        };

        Response.Cookies.Append("AuthToken", request.Token, cookieOptions);
        return Ok(new { success = true });
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken");
        return Redirect("/");
    }

    private void EnsureRolesPopulated(User user)
    {
        if (user.IsAdmin && !user.Roles.Contains("Admin"))
        {
            user.Roles.Add("Admin");
        }
        else if (!user.IsAdmin && user.Roles.Contains("Admin"))
        {
            user.Roles.Remove("Admin");
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private string BuildAbsoluteUrl(string path)
    {
        var configuredBaseUrl = _configuration["APP_BASE_URL"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return $"{configuredBaseUrl.TrimEnd('/')}{path}";
        }

        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        return $"{scheme}://{host}{path}";
    }
}
