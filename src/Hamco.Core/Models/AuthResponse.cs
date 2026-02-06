namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) returned after successful login or registration.
/// Contains the JWT authentication token and user information.
/// </summary>
/// <remarks>
/// This response is sent when:
///   - POST /api/auth/register succeeds
///   - POST /api/auth/login succeeds
/// 
/// Client responsibility:
/// 1. Store the Token securely (localStorage, sessionStorage, or cookie)
/// 2. Include Token in subsequent API requests:
///    Authorization: Bearer {token}
/// 3. Check ExpiresAt and refresh token before expiration
/// 4. Clear token on logout
/// 
/// Security considerations:
///   - Token is a bearer token (anyone with token can use it)
///   - Don't expose in URLs (easy to leak in logs/history)
///   - Use HTTPS always (prevents interception)
///   - Consider refresh tokens for long-lived sessions
/// </remarks>
public class AuthResponse
{
    /// <summary>
    /// JWT (JSON Web Token) for authenticating future requests.
    /// </summary>
    /// <remarks>
    /// JWT structure (3 parts separated by dots):
    /// 
    /// 1. Header: {"alg":"HS256","typ":"JWT"}
    ///    - Algorithm used: HMAC-SHA256
    ///    - Type: JWT token
    /// 
    /// 2. Payload (claims):
    ///    {
    ///      "sub": "a1b2c3d4-...",        // Subject (user ID)
    ///      "email": "user@example.com",   // User email
    ///      "jti": "unique-token-id",      // JWT ID (prevents replay)
    ///      "role": ["Admin", "User"],     // User roles
    ///      "exp": 1675785600              // Expiration timestamp
    ///    }
    /// 
    /// 3. Signature: HMACSHA256(
    ///      base64UrlEncode(header) + "." + base64UrlEncode(payload),
    ///      secret_key
    ///    )
    /// 
    /// Example token:
    ///   eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
    ///   eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.
    ///   SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
    /// 
    /// How to use:
    ///   GET /api/notes
    ///   Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// 
    /// Benefits of JWT:
    ///   - Stateless (no server-side session storage needed)
    ///   - Self-contained (includes user info, no DB lookup)
    ///   - Can be verified with secret key (tamper-proof)
    ///   - Works across distributed systems
    /// 
    /// Drawbacks:
    ///   - Can't be revoked before expiration (unless you maintain blacklist)
    ///   - Larger than session IDs (sent with every request)
    ///   - Secret key must be kept secure (compromise = full system breach)
    /// </remarks>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// User's email address (for display in UI).
    /// </summary>
    /// <remarks>
    /// Included for convenience so client doesn't need to decode JWT.
    /// 
    /// Client can display: "Logged in as user@example.com"
    /// </remarks>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// User's unique identifier (GUID string).
    /// </summary>
    /// <remarks>
    /// Client can use this to:
    ///   - Display user profile (GET /api/users/{userId})
    ///   - Filter user's own posts (GET /api/notes?userId={userId})
    ///   - Track current user in application state
    /// 
    /// Also available in JWT token's "sub" claim, but provided here
    /// for convenience (no need to decode token).
    /// </remarks>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// List of roles assigned to this user (e.g., ["Admin", "User"]).
    /// </summary>
    /// <remarks>
    /// ⚠️ CURRENTLY NOT POPULATED!
    /// 
    /// Roles are in JWT token claims, but AuthController doesn't
    /// set this field in the response.
    /// 
    /// To fix, in AuthController.Register/Login:
    ///   return Ok(new AuthResponse
    ///   {
    ///       Token = token,
    ///       UserId = user.Id,
    ///       Email = user.Email,
    ///       Roles = user.Roles,  // Add this line!
    ///       ExpiresAt = DateTime.UtcNow.AddMinutes(60)
    ///   });
    /// 
    /// Client can use roles for UI decisions:
    ///   if (authResponse.Roles.includes("Admin")) {
    ///       showAdminPanel();
    ///   }
    /// 
    /// Security note: Client-side role checks are for UI only!
    /// Server must ALWAYS validate roles (don't trust client).
    /// </remarks>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// UTC timestamp when this token will expire.
    /// </summary>
    /// <remarks>
    /// ⚠️ CURRENTLY NOT POPULATED!
    /// 
    /// This field is defined but never set in AuthController.
    /// It should be set to token expiration time.
    /// 
    /// To fix, in AuthController.Register/Login:
    ///   ExpiresAt = DateTime.UtcNow.AddMinutes(60)
    /// 
    /// Default token lifetime: 60 minutes (configurable in Program.cs)
    /// 
    /// Client should:
    /// 1. Store this timestamp
    /// 2. Check if token is about to expire before requests
    /// 3. Refresh token or re-login before expiration
    /// 
    /// Example client logic:
    ///   if (DateTime.Now > authResponse.ExpiresAt.AddMinutes(-5)) {
    ///       // Token expires in < 5 minutes, refresh it
    ///       await refreshToken();
    ///   }
    /// 
    /// Token lifetime trade-offs:
    ///   - Short (15 min): More secure, but requires frequent refresh
    ///   - Long (24 hours): Convenient, but higher risk if compromised
    ///   - Best: Short access token + long refresh token
    /// </remarks>
    public DateTime ExpiresAt { get; set; }
}
