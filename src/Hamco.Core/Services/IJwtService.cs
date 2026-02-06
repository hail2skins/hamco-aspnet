using Hamco.Core.Models;
using System.Security.Claims;

namespace Hamco.Core.Services;

/// <summary>
/// Interface for JSON Web Token (JWT) operations.
/// Handles generation and validation of authentication tokens.
/// </summary>
/// <remarks>
/// What is JWT?
///   JWT (JSON Web Token) is a compact, URL-safe means of representing
///   claims to be transferred between two parties.
/// 
/// Why use JWT for authentication?
///   ✅ Stateless: No server-side session storage needed
///   ✅ Self-contained: Token includes user info (no DB lookup)
///   ✅ Scalable: Works across multiple servers (no shared session store)
///   ✅ Mobile-friendly: Works with apps, not just browsers
///   ✅ Cross-domain: Can authenticate across different domains
/// 
/// JWT structure (3 parts separated by dots):
///   {header}.{payload}.{signature}
/// 
///   Example:
///   eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.              ← Header
///   eyJzdWIiOiIxMjM0IiwiZW1haWwiOiJ1c2VyQGV4LmNvbSJ9.    ← Payload (claims)
///   SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c         ← Signature
/// 
/// Authentication flow:
///   1. User logs in with email/password
///   2. Server validates credentials
///   3. Server generates JWT with GenerateToken()
///   4. Client stores token (localStorage, cookie, etc.)
///   5. Client includes token in requests: Authorization: Bearer {token}
///   6. Server validates token with ValidateToken()
///   7. Server extracts user info from token claims
/// 
/// Security considerations:
///   - Token is signed, not encrypted (anyone can read payload)
///   - Don't put sensitive data in token (credit cards, SSNs, etc.)
///   - Secret key must be kept secure (compromise = total breach)
///   - Use HTTPS always (prevent token interception)
///   - Set reasonable expiration time (balance security vs UX)
/// 
/// Why use an interface?
///   - Testability: Mock JWT service in unit tests
///   - Flexibility: Swap implementations (RS256 vs HS256, different libraries)
///   - Dependency Injection: ASP.NET Core can inject this service
/// </remarks>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT token for an authenticated user.
    /// </summary>
    /// <param name="user">The user to generate a token for.</param>
    /// <returns>A signed JWT token string.</returns>
    /// <remarks>
    /// Token includes claims (user information):
    ///   - sub: Subject (user ID)
    ///   - email: User's email address
    ///   - jti: JWT ID (unique token identifier)
    ///   - role: User's roles (for authorization)
    ///   - exp: Expiration timestamp
    /// 
    /// Example token payload (decoded):
    /// {
    ///   "sub": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    ///   "email": "user@example.com",
    ///   "jti": "unique-token-id",
    ///   "role": ["Admin", "User"],
    ///   "exp": 1675785600,
    ///   "iss": "hamco-api",
    ///   "aud": "hamco-client"
    /// }
    /// 
    /// Claims in C#:
    ///   'Claim' is a statement about a user (key-value pair)
    ///   Examples: "email" = "user@example.com", "role" = "Admin"
    ///   Used by ASP.NET Core authorization ([Authorize] attribute)
    /// 
    /// Usage:
    ///   var token = _jwtService.GenerateToken(user);
    ///   return Ok(new AuthResponse { Token = token, ... });
    /// </remarks>
    string GenerateToken(User user);

    /// <summary>
    /// Validates a JWT token and extracts claims.
    /// </summary>
    /// <param name="token">The JWT token string to validate.</param>
    /// <returns>
    /// ClaimsPrincipal containing user claims if token is valid,
    /// null if token is invalid or expired.
    /// </returns>
    /// <remarks>
    /// Validation checks:
    ///   ✅ Signature is correct (token not tampered with)
    ///   ✅ Token not expired (exp claim < current time)
    ///   ✅ Issuer matches (iss claim = expected issuer)
    ///   ✅ Audience matches (aud claim = expected audience)
    /// 
    /// ClaimsPrincipal in C#:
    ///   Represents the identity of a user in ASP.NET Core.
    ///   Contains one or more ClaimsIdentity objects.
    ///   Each ClaimsIdentity contains a collection of Claims.
    /// 
    /// Think of it as:
    ///   ClaimsPrincipal = User
    ///   ClaimsIdentity = Login session (email, JWT, Windows auth, etc.)
    ///   Claim = Individual fact about user (email, role, etc.)
    /// 
    /// The '?' in 'ClaimsPrincipal?' means nullable return type:
    ///   - Returns ClaimsPrincipal if token is valid
    ///   - Returns null if token is invalid/expired
    /// 
    /// This method is called automatically by JWT middleware!
    ///   You typically don't call this directly.
    ///   ASP.NET Core calls it for every request with Authorization header.
    /// 
    /// Usage in middleware:
    ///   var principal = _jwtService.ValidateToken(token);
    ///   if (principal != null) {
    ///       HttpContext.User = principal;  // User is authenticated!
    ///   }
    /// 
    /// Accessing claims in controller:
    ///   var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    ///   var email = User.FindFirst(ClaimTypes.Email)?.Value;
    ///   var isAdmin = User.IsInRole("Admin");
    /// </remarks>
    ClaimsPrincipal? ValidateToken(string token);
}
