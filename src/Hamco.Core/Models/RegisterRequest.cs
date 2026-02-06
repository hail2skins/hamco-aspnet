using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for user registration via POST /api/auth/register.
/// Contains all information needed to create a new user account.
/// </summary>
/// <remarks>
/// Registration flow:
/// 1. Client sends username, email, password (this DTO)
/// 2. AuthController validates input (Data Annotations)
/// 3. Check if email already exists (prevent duplicates)
/// 4. Hash password with BCrypt
/// 5. Create User entity and save to database
/// 6. Generate JWT token for immediate login
/// 7. Return token to client (AuthResponse)
/// 
/// Why immediate login after registration?
///   - Better user experience (one-step process)
///   - Industry standard (most apps do this)
///   - User doesn't have to enter credentials again
/// 
/// For production, add email verification before allowing full access.
/// </remarks>
public class RegisterRequest
{
    /// <summary>
    /// Desired username (display name, not used for login).
    /// </summary>
    /// <remarks>
    /// [Required]: Username must be provided
    /// 
    /// Username vs Email distinction:
    ///   - Username: Public display name ("johndoe", "Jane Smith")
    ///   - Email: Private login identifier (john@example.com)
    /// 
    /// This allows users to:
    ///   - Change display name without affecting login
    ///   - Keep email private (username shown in posts)
    ///   - Use friendly names instead of email addresses
    /// 
    /// Current limitation: No uniqueness check!
    /// Multiple users can have the same username.
    /// Future: Add username uniqueness constraint.
    /// 
    /// Validation we should add:
    ///   - [StringLength(50, MinimumLength = 3)]
    ///   - [RegularExpression(@"^[a-zA-Z0-9_-]+$")] (alphanumeric + _ -)
    /// </remarks>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's email address (must be unique, used for login).
    /// </summary>
    /// <remarks>
    /// [Required]: Email must be provided
    /// [EmailAddress]: Must be valid email format
    /// 
    /// Email uniqueness is checked in AuthController.Register():
    ///   if (await _context.Users.AnyAsync(u => u.Email == request.Email))
    ///       return BadRequest(new { message = "Email already exists" });
    /// 
    /// Why not use [UniqueEmail] attribute?
    ///   - No built-in attribute for database uniqueness checks
    ///   - Uniqueness is a database concern, not just validation
    ///   - Custom attributes would need database access (breaks separation)
    /// 
    /// Better approach: Check in controller + database unique constraint
    /// (Both are implemented!)
    /// 
    /// Future improvements:
    ///   - Email normalization (convert to lowercase)
    ///   - Email verification (send confirmation link)
    ///   - Disposable email detection
    /// </remarks>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's desired password (will be hashed before storage).
    /// </summary>
    /// <remarks>
    /// [Required]: Password must be provided
    /// [MinLength(6)]: Minimum 6 characters
    /// 
    /// What happens to this password:
    /// 1. Transmitted over HTTPS (encrypted in transit)
    /// 2. Received by AuthController.Register()
    /// 3. Passed to PasswordHasher.HashPassword()
    /// 4. BCrypt generates hash with random salt
    /// 5. Hash stored in database (original password discarded!)
    /// 
    /// Example transformation:
    ///   Input:  "myPassword123"
    ///   Output: "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW"
    /// 
    /// The hash includes:
    ///   - $2a$ = BCrypt algorithm version
    ///   - 12 = Work factor (2^12 iterations = ~300ms)
    ///   - Next 22 chars = Salt (random, unique per password)
    ///   - Remaining chars = Actual hash
    /// 
    /// This means:
    ///   - Same password for different users = different hashes (salt)
    ///   - Cannot reverse the hash to get original password
    ///   - Brute force takes years even with fast hardware
    /// 
    /// Password best practices for production:
    ///   - Minimum 8 characters (preferably 12+)
    ///   - Require mix of character types
    ///   - Check against breached password lists (HaveIBeenPwned API)
    ///   - Add password strength meter in UI
    ///   - Consider passphrase support (4+ random words)
    /// </remarks>
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}
