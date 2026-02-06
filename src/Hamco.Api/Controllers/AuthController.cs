using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;

namespace Hamco.Api.Controllers;

/// <summary>
/// API controller for user authentication and registration.
/// Handles user account creation and login with JWT token generation.
/// </summary>
/// <remarks>
/// REST API endpoints:
///   POST /api/auth/register - Create new user account
///   POST /api/auth/login    - Authenticate user and get JWT token
/// 
/// Authentication flow:
/// 1. User registers (POST /api/auth/register)
///    → Password hashed with BCrypt
///    → User saved to database
///    → JWT token generated and returned
/// 
/// 2. User logs in (POST /api/auth/login)
///    → Email lookup in database
///    → Password verification with BCrypt
///    → JWT token generated and returned
/// 
/// 3. User makes authenticated requests
///    → Include token: Authorization: Bearer {token}
///    → JWT middleware validates token
///    → User identity available in controllers
/// 
/// Security features:
///   ✅ Passwords hashed with BCrypt (never stored in plain text)
///   ✅ JWT tokens for stateless authentication
///   ✅ Email uniqueness enforced (prevents duplicate accounts)
///   ✅ Password verification constant-time (prevents timing attacks)
/// 
/// ⚠️ Current limitation:
///   Authentication is implemented but NOT enforced on note endpoints.
///   Notes can be created/modified by anyone (anonymous access).
///   Future: Add [Authorize] attribute to NotesController actions.
/// </remarks>
[ApiController]
[Route("api/[controller]")]  // Route: /api/auth
public class AuthController : ControllerBase
{
    // Dependencies injected via constructor
    private readonly HamcoDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    /// <summary>
    /// Initializes a new instance of the AuthController.
    /// </summary>
    /// <param name="context">Database context for user data access.</param>
    /// <param name="passwordHasher">Service for hashing and verifying passwords.</param>
    /// <param name="jwtService">Service for generating and validating JWT tokens.</param>
    /// <remarks>
    /// Multiple dependency injection:
    ///   This controller requires 3 services:
    ///   1. HamcoDbContext: Database access (user lookup, creation)
    ///   2. IPasswordHasher: Password security (hash, verify)
    ///   3. IJwtService: Token generation (JWT creation)
    /// 
    /// All dependencies registered in Program.cs:
    ///   - AddDbContext&lt;HamcoDbContext&gt;: Database context
    ///   - AddAuthServices(): Password hasher + JWT service
    /// 
    /// DI container creates instances and injects them automatically.
    /// 
    /// Why use interfaces (IPasswordHasher, IJwtService)?
    ///   - Testability: Can inject mock implementations in tests
    ///   - Flexibility: Can swap implementations without changing controller
    ///   - Dependency Inversion Principle: Depend on abstractions, not concretions
    /// </remarks>
    public AuthController(
        HamcoDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">
    /// Registration data (username, email, password).
    /// Validated automatically via Data Annotations.
    /// </param>
    /// <returns>
    /// 200 OK with JWT token if successful.
    /// 400 Bad Request if email already exists or validation fails.
    /// </returns>
    /// <remarks>
    /// HTTP Method: POST /api/auth/register
    /// 
    /// Request body (JSON):
    /// {
    ///   "username": "johndoe",
    ///   "email": "john@example.com",
    ///   "password": "securePassword123"
    /// }
    /// 
    /// Response (200 OK):
    /// {
    ///   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///   "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    ///   "email": "john@example.com",
    ///   "roles": [],
    ///   "expiresAt": null  ← ⚠️ Not populated (should be fixed)
    /// }
    /// 
    /// Registration process:
    /// 1. Validate input (automatic via Data Annotations)
    /// 2. Check if email already exists (prevent duplicates)
    /// 3. Hash password with BCrypt (original password discarded)
    /// 4. Create user entity
    /// 5. Save to database
    /// 6. Generate JWT token
    /// 7. Return token to client (immediate login)
    /// 
    /// Why immediate login after registration?
    ///   Better UX: User doesn't need to login separately
    ///   Industry standard: Most apps do this (Twitter, Facebook, etc.)
    ///   Token returned: Client stores it and is authenticated
    /// 
    /// Security considerations:
    ///   ✅ Password hashed before storage (BCrypt, work factor 12)
    ///   ✅ Email uniqueness checked (prevents duplicate accounts)
    ///   ⚠️ No email verification (should send confirmation email)
    ///   ⚠️ Weak password rules (6 chars minimum, no complexity)
    ///   ⚠️ No rate limiting (vulnerable to spam registrations)
    /// 
    /// BadRequest() explained:
    ///   Returns 400 Bad Request with custom error object.
    ///   Anonymous object: new { message = "..." }
    ///   Serialized to JSON: {"message":"Email already exists"}
    /// </remarks>
    [HttpPost("register")]  // Route: POST /api/auth/register
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Step 1: Check if email already exists
        // AnyAsync(): Returns true if any user matches condition
        // Lambda: u => u.Email == request.Email
        //   'u': User parameter
        //   'u.Email == request.Email': Condition to check
        // 
        // SQL generated:
        // SELECT CASE WHEN EXISTS(
        //   SELECT 1 FROM users WHERE email = ?
        // ) THEN 1 ELSE 0 END;
        // 
        // Performance: Uses EXISTS (stops at first match, efficient)
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            // Email exists, return 409 Conflict (more semantically correct than 400)
            // Conflict() returns 409 status code
            // Anonymous object: new { property = value }
            //   Creates object with 'message' property
            //   Serialized to JSON: {"message":"Email already exists"}
            return Conflict(new { message = "Email already exists" });
        }

        // Step 2: Check if this is the first user (should be admin)
        var isFirstUser = !await _context.Users.AnyAsync();
        
        // Step 3: Create new user entity
        // Note: Password is HASHED, not stored in plain text!
        var user = new User
        {
            // Id generated automatically (Guid.NewGuid() in User model)
            Username = request.Username,
            Email = request.Email,
            
            // CRITICAL: Hash password before storing!
            // _passwordHasher.HashPassword() uses BCrypt:
            //   Input:  "myPassword123"
            //   Output: "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW"
            // Original password is NEVER stored or logged!
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            
            CreatedAt = DateTime.UtcNow,
            
            // First user automatically becomes admin
            IsAdmin = isFirstUser,
            
            // Email not verified yet (future: send verification email via Mailjet)
            IsEmailVerified = false,
            
            // Roles left empty (default: new List<string>())
        };

        // Step 4: Add user to database
        _context.Users.Add(user);
        
        // Step 5: Save changes (execute INSERT)
        // SQL: INSERT INTO users (id, username, email, password_hash, created_at, is_admin, is_email_verified)
        //      VALUES (?, ?, ?, ?, ?, ?, ?);
        await _context.SaveChangesAsync();

        // Step 6: Generate JWT token
        // Token includes user ID, email, roles (if any)
        // See JwtService.GenerateToken() for details
        var token = _jwtService.GenerateToken(user);

        // Step 7: Return 201 Created with authentication response
        // CreatedAtAction returns 201 with Location header pointing to resource
        // For auth endpoints, we return the response directly (no specific GET endpoint)
        return CreatedAtAction(nameof(Register), new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles,  // Empty list by default
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)  // Token expires in 60 minutes
        });
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">
    /// Login credentials (email and password).
    /// Validated automatically via Data Annotations.
    /// </param>
    /// <returns>
    /// 200 OK with JWT token if credentials are valid.
    /// 401 Unauthorized if email not found or password incorrect.
    /// 400 Bad Request if validation fails.
    /// </returns>
    /// <remarks>
    /// HTTP Method: POST /api/auth/login
    /// 
    /// Request body (JSON):
    /// {
    ///   "email": "john@example.com",
    ///   "password": "securePassword123"
    /// }
    /// 
    /// Response (200 OK):
    /// {
    ///   "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///   "userId": "a1b2c3d4-...",
    ///   "email": "john@example.com",
    ///   "roles": [],
    ///   "expiresAt": null
    /// }
    /// 
    /// Response (401 Unauthorized):
    /// {
    ///   "message": "Invalid email or password"
    /// }
    /// 
    /// Login process:
    /// 1. Validate input (automatic)
    /// 2. Look up user by email
    /// 3. Verify password against stored hash
    /// 4. Generate JWT token if valid
    /// 5. Return token to client
    /// 
    /// Security best practices:
    ///   ✅ Generic error message (don't reveal if email exists)
    ///   ✅ Constant-time password verification (BCrypt.Verify)
    ///   ✅ Password never logged or exposed
    ///   ⚠️ No rate limiting (vulnerable to brute force)
    ///   ⚠️ No account lockout after failed attempts
    ///   ⚠️ No multi-factor authentication (MFA)
    /// 
    /// Why generic error message?
    ///   Bad: "Email not found" vs "Incorrect password"
    ///     → Attacker learns which emails are registered
    ///     → Can enumerate valid accounts
    ///   
    ///   Good: "Invalid email or password"
    ///     → Same message for both cases
    ///     → Attacker can't determine if email exists
    /// 
    /// Unauthorized() vs BadRequest():
    ///   Unauthorized (401): Authentication failed (wrong credentials)
    ///   BadRequest (400): Validation failed (malformed request)
    ///   We use Unauthorized for invalid credentials (correct HTTP semantics).
    /// </remarks>
    [HttpPost("login")]  // Route: POST /api/auth/login
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        // Step 1: Find user by email
        // FirstOrDefaultAsync(): Returns first matching user, or null if not found
        // Lambda: u => u.Email == request.Email
        // 
        // SQL: SELECT * FROM users WHERE email = ? LIMIT 1;
        // 
        // Why FirstOrDefaultAsync instead of FindAsync?
        //   FindAsync: Lookup by primary key (Id)
        //   FirstOrDefaultAsync: Lookup by any field (Email)
        // 
        // Email should be unique (database constraint recommended)
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Step 2: Verify user exists and password is correct
        // Two checks combined with logical AND (&&) and NOT (!):
        //   user == null: No user with that email
        //   !_passwordHasher.VerifyPassword(...): Password doesn't match
        // 
        // VerifyPassword() process:
        //   1. Extract salt from stored hash
        //   2. Hash input password with same salt
        //   3. Compare hashes (constant-time comparison)
        //   4. Return true if match, false otherwise
        // 
        // Short-circuit evaluation:
        //   If user == null, second condition not evaluated (prevents null reference)
        //   C# evaluates left-to-right and stops when result is determined
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // Authentication failed - generic error message
            // Don't reveal whether email exists or password is wrong!
            // 
            // Unauthorized() returns 401 Unauthorized status code
            // Common misconception: 401 means "not authorized"
            // Correct meaning: 401 means "not authenticated" (who are you?)
            // 403 Forbidden means "not authorized" (I know who you are, but you can't do this)
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Step 3: Generate JWT token for authenticated user
        var token = _jwtService.GenerateToken(user);

        // Step 4: Return authentication response
        // Same response as Register (consistent API)
        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    /// <summary>
    /// Gets the profile of the currently authenticated user.
    /// </summary>
    /// <returns>
    /// 200 OK with user profile if authenticated.
    /// 401 Unauthorized if not authenticated.
    /// </returns>
    /// <remarks>
    /// HTTP Method: GET /api/auth/profile
    /// 
    /// Requires: Authorization: Bearer {token} header
    /// 
    /// This endpoint demonstrates JWT authentication:
    /// 1. Client sends JWT token in Authorization header
    /// 2. JWT middleware validates token signature
    /// 3. If valid, extracts user claims from token
    /// 4. Makes user identity available via User property
    /// 5. Controller extracts user ID from claims
    /// 6. Looks up full user data from database
    /// 7. Returns user profile (excluding password hash)
    /// 
    /// Security: Password hash is NEVER returned in API responses!
    /// </remarks>
    [HttpGet("profile")]  // Route: GET /api/auth/profile
    [Authorize]  // Requires valid JWT token
    public async Task<ActionResult<AuthResponse>> GetProfile()
    {
        // Extract user ID from JWT token claims
        // User.FindFirst() looks for claim by type
        // ClaimTypes.NameIdentifier = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        // This is where we store the user ID in JwtService.GenerateToken()
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            // Token is valid but doesn't contain user ID claim (shouldn't happen)
            return Unauthorized(new { message = "Invalid token claims" });
        }

        // Look up user in database by ID
        var user = await _context.Users.FindAsync(userIdClaim);
        
        if (user == null)
        {
            // User was deleted after token was issued
            return Unauthorized(new { message = "User not found" });
        }

        // Return user profile (same format as login/register for consistency)
        // Password hash is NOT included!
        return Ok(new AuthResponse
        {
            Token = string.Empty,  // Don't issue new token on profile request
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles,
            ExpiresAt = DateTime.MinValue  // Client should use existing token expiry
        });
    }

    /// <summary>
    /// Stub endpoint for forgot password (future Mailjet integration).
    /// </summary>
    /// <param name="request">Email address to send reset link to.</param>
    /// <returns>200 OK with message about future implementation.</returns>
    /// <remarks>
    /// HTTP Method: POST /api/auth/forgot-password
    /// 
    /// Future implementation with Mailjet:
    /// 1. Validate email exists in database
    /// 2. Generate secure reset token (GUID or signed JWT)
    /// 3. Store token in database with expiration (e.g., 1 hour)
    /// 4. Send email via Mailjet with reset link
    /// 5. Link contains token: /reset-password?token=xxx
    /// 6. Return success message (don't reveal if email exists!)
    /// 
    /// Security: Always return success even if email doesn't exist
    /// (prevents email enumeration attacks)
    /// </remarks>
    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] dynamic request)
    {
        // Stub implementation - always returns success
        // In production, send password reset email via Mailjet
        return Ok(new 
        { 
            message = "Password reset email sent (not implemented yet). " +
                      "Future: Will integrate with Mailjet to send reset link."
        });
    }

    /// <summary>
    /// Stub endpoint for reset password (future Mailjet integration).
    /// </summary>
    /// <param name="request">Reset token and new password.</param>
    /// <returns>200 OK with message about future implementation.</returns>
    /// <remarks>
    /// HTTP Method: POST /api/auth/reset-password
    /// 
    /// Future implementation:
    /// 1. Validate reset token from database
    /// 2. Check token hasn't expired
    /// 3. Hash new password with BCrypt
    /// 4. Update user's password hash
    /// 5. Invalidate reset token
    /// 6. Optionally send confirmation email
    /// 7. Return success message
    /// 
    /// Security: Use constant-time token comparison
    /// Invalidate token after use (one-time use only)
    /// </remarks>
    [HttpPost("reset-password")]
    public IActionResult ResetPassword([FromBody] dynamic request)
    {
        // Stub implementation
        // In production, validate token and update password
        return Ok(new 
        { 
            message = "Password reset (not implemented yet). " +
                      "Future: Will validate token and update password hash."
        });
    }
}
