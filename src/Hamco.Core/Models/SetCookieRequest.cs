using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for setting authentication cookie via POST /api/auth/cookie.
/// Contains JWT token that will be stored in HTTP-only cookie.
/// </summary>
/// <remarks>
/// Cookie-based authentication flow:
/// 1. User logs in via /api/auth/login → receives JWT token
/// 2. Login page JavaScript calls /api/auth/cookie with token
/// 3. Server sets HTTP-only cookie with JWT
/// 4. Browser automatically includes cookie in subsequent requests
/// 5. Server validates JWT from cookie → User.Identity.IsAuthenticated = true
/// 6. Nav bar shows authenticated state (server-side rendering)
/// 
/// Why use cookies instead of localStorage?
///   ✅ Automatic: Browser sends cookie with every request (no JS needed)
///   ✅ Server-side: Works with SSR (User.Identity available in Razor views)
///   ✅ Secure: HTTP-only cookies can't be accessed by JavaScript (XSS protection)
///   ✅ Standard: Well-established pattern for web authentication
/// 
/// Security benefits:
///   - HTTP-only flag prevents XSS attacks (malicious JS can't steal token)
///   - Secure flag ensures HTTPS-only transmission in production
///   - SameSite=Strict prevents CSRF attacks
///   - Automatic expiration matches JWT token lifetime
/// </remarks>
public class SetCookieRequest
{
    /// <summary>
    /// JWT token to store in HTTP-only cookie.
    /// </summary>
    /// <remarks>
    /// [Required]: Token must be provided
    /// 
    /// Expected format:
    ///   eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
    /// 
    /// JWT structure (3 parts, base64-encoded, separated by dots):
    ///   1. Header: Algorithm and token type
    ///   2. Payload: Claims (user ID, email, roles, expiration)
    ///   3. Signature: HMAC hash to verify authenticity
    /// 
    /// The token is validated by ASP.NET Core JWT middleware:
    ///   - Signature checked against secret key
    ///   - Expiration checked (reject expired tokens)
    ///   - Issuer/Audience checked (prevent token reuse across apps)
    ///   - If valid, claims extracted and User.Identity populated
    /// 
    /// See Program.cs AddAuthServices() for JWT validation configuration
    /// See JwtService.GenerateToken() for token creation logic
    /// </remarks>
    [Required]
    public string Token { get; set; } = string.Empty;
}
