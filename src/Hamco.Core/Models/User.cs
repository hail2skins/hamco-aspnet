namespace Hamco.Core.Models;

/// <summary>
/// Represents a user account in the system.
/// Users can register, login, and create notes (blog posts).
/// </summary>
/// <remarks>
/// Maps to the 'users' table in PostgreSQL database.
/// 
/// Security features:
/// - Passwords are hashed with BCrypt (never stored in plain text)
/// - JWT tokens used for authentication (stateless)
/// - Email addresses must be unique (enforced at DB level)
/// 
/// In C#, classes are reference types (stored on heap, passed by reference).
/// When you pass a User object to a method, you're passing a reference,
/// not a copy of the entire object.
/// </remarks>
public class User
{
    /// <summary>
    /// Primary key. A globally unique identifier (GUID) string.
    /// </summary>
    /// <remarks>
    /// GUIDs (Globally Unique Identifiers) are 128-bit values like:
    ///   "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    /// 
    /// Why GUID instead of int?
    ///   - Prevents user enumeration attacks (can't guess /api/users/1, /api/users/2)
    ///   - Can generate IDs client-side without database roundtrip
    ///   - Unique across distributed systems
    /// 
    /// 'Guid.NewGuid().ToString()' generates a new random GUID.
    /// This is set as default value, but can be overridden.
    /// </remarks>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Display name for the user (e.g., "johndoe", "Jane Smith").
    /// Not used for login - email is used instead.
    /// </summary>
    /// <remarks>
    /// Username vs Email:
    ///   - Username: Display name (can be changed)
    ///   - Email: Login identifier (should be unique and verified)
    /// 
    /// Default value 'string.Empty' prevents null reference warnings in C# 8+.
    /// Validation enforced via RegisterRequest DTO.
    /// </remarks>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// User's email address. Used for login and must be unique.
    /// </summary>
    /// <remarks>
    /// Email is the primary login credential.
    /// Uniqueness enforced at database level (prevents duplicate accounts).
    /// 
    /// Validation:
    ///   - Format checked via [EmailAddress] attribute on RegisterRequest
    ///   - Uniqueness checked in AuthController.Register()
    /// 
    /// Future improvements:
    ///   - Email verification (send confirmation link)
    ///   - Email normalization (lowercase, trim)
    /// </remarks>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// BCrypt hash of the user's password. NEVER store plain text passwords!
    /// </summary>
    /// <remarks>
    /// Password security explained:
    /// 
    /// 1. User registers with password: "mySecretPassword123"
    /// 2. PasswordHasher.HashPassword() creates BCrypt hash:
    ///    "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW"
    /// 3. Only the hash is stored (56 chars, includes salt)
    /// 4. Original password is discarded
    /// 
    /// When user logs in:
    /// 1. User submits password: "mySecretPassword123"
    /// 2. PasswordHasher.VerifyPassword() checks if it matches the hash
    /// 3. Returns true/false (never reveals the original password)
    /// 
    /// Why BCrypt?
    ///   - Slow by design (protects against brute force attacks)
    ///   - Includes random salt (prevents rainbow table attacks)
    ///   - Work factor 12 = ~300ms per hash (adjustable for future hardware)
    /// 
    /// Even if database is compromised, passwords remain secure!
    /// </remarks>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// List of roles assigned to this user (e.g., "Admin", "Editor", "User").
    /// Used for authorization (what users can do).
    /// </summary>
    /// <remarks>
    /// In C#, 'List&lt;string&gt;' is a dynamic array:
    ///   - Can grow/shrink in size
    ///   - Access items: roles[0], roles[1]
    ///   - Add items: roles.Add("Admin")
    /// 
    /// 'new()' is shorthand for 'new List&lt;string&gt;()' (C# 9+).
    /// 
    /// ⚠️ IMPORTANT: Roles are NOT stored in database!
    /// See HamcoDbContext - entity.Ignore(e => e.Roles)
    /// 
    /// This field is populated from JWT token claims when user is authenticated.
    /// Future: Create separate 'user_roles' table for persistent role storage.
    /// 
    /// Authorization example:
    ///   [Authorize(Roles = "Admin")]  // Only admin users can access
    ///   public async Task&lt;IActionResult&gt; DeleteUser(string id) { ... }
    /// </remarks>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// Indicates whether the user's email address has been verified.
    /// </summary>
    /// <remarks>
    /// Email verification flow (future Mailjet integration):
    /// 1. User registers → IsEmailVerified = false
    /// 2. Send verification email with unique token
    /// 3. User clicks link → validate token → set IsEmailVerified = true
    /// 
    /// Security: Unverified users could be restricted from certain actions
    /// (e.g., posting notes, changing sensitive settings)
    /// 
    /// Default: false (users must verify email)
    /// </remarks>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// SHA-256 hash of the current email verification token.
    /// Empty when no active verification token exists.
    /// </summary>
    public string? EmailVerificationTokenHash { get; set; }

    /// <summary>
    /// UTC expiration timestamp for the email verification token.
    /// </summary>
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>
    /// SHA-256 hash of the current password reset token.
    /// Empty when no active reset token exists.
    /// </summary>
    public string? PasswordResetTokenHash { get; set; }

    /// <summary>
    /// UTC expiration timestamp for the password reset token.
    /// </summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    
    /// <summary>
    /// Indicates whether the user has administrator privileges.
    /// </summary>
    /// <remarks>
    /// Admin users can:
    /// - Create/edit/delete any notes (including other users' notes)
    /// - Manage user accounts
    /// - Access admin-only endpoints
    /// 
    /// First registered user should automatically be admin.
    /// Additional admins can be promoted via admin panel or database.
    /// 
    /// This field will be stored in database and also included in JWT claims
    /// for quick authorization checks without database lookup.
    /// 
    /// Default: false (normal user)
    /// </remarks>
    public bool IsAdmin { get; set; } = false;
    
    /// <summary>
    /// UTC timestamp when this user account was created.
    /// </summary>
    /// <remarks>
    /// Always use UTC (Coordinated Universal Time) for stored timestamps:
    ///   - Avoids timezone confusion
    ///   - Easy to convert to local time in UI
    ///   - Consistent across servers in different locations
    /// 
    /// DateTime in C# is a value type (struct):
    ///   - Stored on stack (if local variable) or inline in object
    ///   - Default value is DateTime.MinValue (0001-01-01 00:00:00)
    ///   - Can be nullable: DateTime?
    /// 
    /// Database also has DEFAULT CURRENT_TIMESTAMP for redundancy.
    /// </remarks>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
