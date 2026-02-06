using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for user login via POST /api/auth/login.
/// Contains credentials that will be validated against stored user data.
/// </summary>
/// <remarks>
/// Login flow:
/// 1. Client sends email + password (this DTO)
/// 2. AuthController looks up user by email
/// 3. PasswordHasher verifies password against stored hash
/// 4. If valid, JwtService generates authentication token
/// 5. Token returned to client (AuthResponse)
/// 6. Client includes token in future requests
/// 
/// Security note: Passwords are NEVER logged or stored anywhere!
/// They exist only in memory during the login request.
/// </remarks>
public class LoginRequest
{
    /// <summary>
    /// User's email address (used as login identifier).
    /// </summary>
    /// <remarks>
    /// [Required]: Email must be provided
    /// [EmailAddress]: Must be valid email format (checked via regex)
    /// 
    /// Valid formats:
    ///   ✅ user@example.com
    ///   ✅ john.doe@company.co.uk
    ///   ❌ invalid-email
    ///   ❌ @example.com
    /// 
    /// The [EmailAddress] attribute uses this pattern:
    ///   ^[^@\s]+@[^@\s]+\.[^@\s]+$
    /// 
    /// It's basic validation - doesn't verify email exists!
    /// For production, send verification emails.
    /// </remarks>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's password in plain text (transmitted securely over HTTPS).
    /// </summary>
    /// <remarks>
    /// [Required]: Password must be provided
    /// [MinLength(6)]: Minimum 6 characters (weak but acceptable for demo)
    /// 
    /// Security layers:
    /// 1. HTTPS: Password encrypted in transit (TLS/SSL)
    /// 2. No logging: Password never written to logs
    /// 3. Memory only: Password exists only during request processing
    /// 4. Comparison: BCrypt.Verify() checks hash, discards password
    /// 
    /// Why MinLength(6) is weak:
    ///   - "123456" would pass validation
    ///   - No complexity requirements (uppercase, numbers, symbols)
    /// 
    /// Production password rules should require:
    ///   - 8+ characters
    ///   - Mix of uppercase, lowercase, numbers, symbols
    ///   - Not in common password lists
    ///   - Use libraries like Zxcvbn for strength checking
    /// 
    /// For learning purposes, 6 chars is fine!
    /// </remarks>
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}
