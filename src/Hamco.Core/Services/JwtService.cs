using Hamco.Core.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Hamco.Core.Services;

/// <summary>
/// JWT (JSON Web Token) service implementation for authentication.
/// Generates and validates JWT tokens using HMAC-SHA256 algorithm.
/// </summary>
/// <remarks>
/// This implementation uses:
///   - HS256 algorithm (HMAC with SHA-256)
///   - Symmetric key (same secret for signing and validation)
///   - Microsoft's System.IdentityModel.Tokens.Jwt library
/// 
/// Token security model:
///   - Secret key must be ≥ 32 characters (enforced in constructor)
///   - Tokens expire after configurable time (default 60 minutes)
///   - Tokens include issuer/audience validation (prevents misuse)
///   - Signature prevents tampering (any change invalidates token)
/// 
/// Alternative approaches:
///   - RS256 (asymmetric): Public/private key pair (better for microservices)
///   - HS384/HS512: Stronger hashing (slower, usually unnecessary)
///   - OAuth2/OpenID Connect: Industry-standard protocols (more complex)
/// 
/// This class is registered as a Singleton in DI container:
///   - One instance shared across entire application
///   - Thread-safe (JwtSecurityTokenHandler is thread-safe)
///   - No state stored (stateless token generation)
/// </remarks>
public class JwtService : IJwtService
{
    // Private fields store configuration values
    // 'readonly' means they can only be set in constructor (immutable after creation)
    // Naming convention: _camelCase for private fields
    
    /// <summary>
    /// Secret key used for signing and validating tokens.
    /// Must be kept secure and never exposed publicly.
    /// </summary>
    private readonly string _secret;
    
    /// <summary>
    /// Token issuer (who created the token).
    /// Validates that token came from our server.
    /// </summary>
    private readonly string _issuer;
    
    /// <summary>
    /// Token audience (who should use the token).
    /// Validates that token is intended for our application.
    /// </summary>
    private readonly string _audience;
    
    /// <summary>
    /// Token lifetime in minutes. Default 60 minutes (1 hour).
    /// </summary>
    private readonly int _expirationMinutes;

    /// <summary>
    /// Initializes a new instance of the JwtService.
    /// </summary>
    /// <param name="secret">
    /// Secret key for signing tokens. Must be at least 32 characters.
    /// </param>
    /// <param name="issuer">
    /// Token issuer identifier (e.g., "hamco-api").
    /// </param>
    /// <param name="audience">
    /// Token audience identifier (e.g., "hamco-client").
    /// </param>
    /// <param name="expirationMinutes">
    /// Token lifetime in minutes. Default is 60 (1 hour).
    /// </param>
    /// <remarks>
    /// Constructor parameters with defaults in C#:
    ///   - 'int expirationMinutes = 60' means this parameter is optional
    ///   - If not provided, defaults to 60
    ///   - Syntax: parameterName = defaultValue
    /// 
    /// Example calls:
    ///   new JwtService("key", "issuer", "audience")           → 60 min expiration
    ///   new JwtService("key", "issuer", "audience", 120)      → 120 min expiration
    /// 
    /// Why validate secret key length?
    ///   HMAC-SHA256 security depends on key length:
    ///   - 256 bits (32 bytes) = full strength
    ///   - Shorter keys = weaker security (easier to brute force)
    ///   - We enforce minimum 32 characters for security
    /// 
    /// ArgumentException in C#:
    ///   - Thrown when parameter value is invalid
    ///   - 'nameof(secret)' gets parameter name as string (refactoring-safe)
    ///   - Exception stops constructor, object is not created
    /// </remarks>
    public JwtService(string secret, string issuer, string audience, int expirationMinutes = 60)
    {
        // Guard clause: validate secret key length
        // 'string.IsNullOrWhiteSpace()' checks for null, empty, or whitespace
        // '||' is the logical OR operator (either condition triggers)
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new ArgumentException("JWT secret must be at least 32 characters long", nameof(secret));

        // Store constructor parameters in private fields
        // 'this._secret' is redundant (can just write '_secret')
        // But included for clarity in learning code
        _secret = secret;
        _issuer = issuer;
        _audience = audience;
        _expirationMinutes = expirationMinutes;
    }

    /// <summary>
    /// Generates a signed JWT token for the specified user.
    /// </summary>
    /// <param name="user">User to create token for.</param>
    /// <returns>JWT token string (3 parts: header.payload.signature).</returns>
    /// <remarks>
    /// Token generation process:
    /// 1. Convert secret string to bytes (HMAC requires byte array)
    /// 2. Create signing key (SymmetricSecurityKey)
    /// 3. Create signing credentials (algorithm + key)
    /// 4. Build list of claims (user info)
    /// 5. Create JWT security token (header + payload)
    /// 6. Sign token and serialize to string
    /// 
    /// Generated token structure:
    /// {
    ///   "alg": "HS256",                    ← Signing algorithm
    ///   "typ": "JWT"                       ← Token type
    /// }
    /// {
    ///   "sub": "user-id",                  ← Subject (user ID)
    ///   "email": "user@example.com",       ← Email
    ///   "jti": "unique-id",                ← JWT ID
    ///   "role": ["Admin", "User"],         ← Roles
    ///   "exp": 1675785600,                 ← Expiration timestamp
    ///   "iss": "hamco-api",                ← Issuer
    ///   "aud": "hamco-client"              ← Audience
    /// }
    /// {signature}                          ← HMAC-SHA256(header + payload, secret)
    /// </remarks>
    public string GenerateToken(User user)
    {
        // Step 1: Convert secret string to byte array
        // Encoding.UTF8.GetBytes() converts string to UTF-8 encoded bytes
        // Example: "secret" → [115, 101, 99, 114, 101, 116]
        // Why bytes? Cryptographic operations work on bytes, not strings
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        
        // Step 2: Create signing credentials
        // Combines the key with the signing algorithm (HMAC-SHA256)
        // 'SecurityAlgorithms.HmacSha256' is a constant string "HS256"
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Step 3: Build list of claims (user information in token)
        // 'new List<Claim>' creates a new list of claims
        // Collection initializer syntax: { item1, item2, ... }
        var claims = new List<Claim>
        {
            // Standard JWT claims (defined in RFC 7519)
            // JwtRegisteredClaimNames contains constants for standard claims
            
            // "sub" (Subject): Unique user identifier
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            
            // "email": User's email address
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            
            // "jti" (JWT ID): Unique token identifier (prevents replay attacks)
            // Guid.NewGuid().ToString() creates a new GUID like "a1b2c3d4-..."
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            
            // ClaimTypes are ASP.NET Core specific claim types
            // These are used by [Authorize] attribute for authorization
            
            // User.FindFirst(ClaimTypes.NameIdentifier) will find this claim
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            
            // User.FindFirst(ClaimTypes.Email) will find this claim
            new Claim(ClaimTypes.Email, user.Email)
        };

        // Step 4: Add role claims for authorization
        // Add Admin role if user is an administrator
        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }
        
        // 'foreach' loop iterates over each additional role in user.Roles list
        // Syntax: foreach (type variable in collection) { ... }
        foreach (var role in user.Roles)
        {
            // Add a role claim for each role
            // Multiple roles = multiple claims with same type (ClaimTypes.Role)
            // Example: If user.Roles = ["Admin", "Editor"]
            //   Adds: new Claim(ClaimTypes.Role, "Admin")
            //   Adds: new Claim(ClaimTypes.Role, "Editor")
            // 
            // Later, User.IsInRole("Admin") checks for this claim
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Step 5: Create the JWT security token
        // JwtSecurityToken is the main token object
        // Named parameters make code self-documenting
        var token = new JwtSecurityToken(
            issuer: _issuer,              // Who created this token
            audience: _audience,          // Who should use this token
            claims: claims,               // User information (from above)
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),  // When token expires
            signingCredentials: credentials  // How to sign the token
        );
        
        // Note: 'expires' uses DateTime.UtcNow (current UTC time)
        // AddMinutes() adds time to the current timestamp
        // Example: UtcNow = 2026-02-06 14:00:00
        //          AddMinutes(60) = 2026-02-06 15:00:00

        // Step 6: Serialize token to string format
        // JwtSecurityTokenHandler converts JwtSecurityToken object
        // to the standard JWT string format (header.payload.signature)
        // WriteToken() performs:
        //   1. Base64Url encode header
        //   2. Base64Url encode payload
        //   3. Create signature: HMAC-SHA256(header + "." + payload, secret)
        //   4. Base64Url encode signature
        //   5. Concatenate: header.payload.signature
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT token and extracts user claims.
    /// </summary>
    /// <param name="token">JWT token string to validate.</param>
    /// <returns>ClaimsPrincipal if valid, null if invalid or expired.</returns>
    /// <remarks>
    /// Validation checks performed:
    ///   1. Signature verification (token not tampered with)
    ///   2. Expiration check (token not expired)
    ///   3. Issuer verification (token from correct source)
    ///   4. Audience verification (token for correct app)
    ///   5. Lifetime validation (token within valid time window)
    /// 
    /// Returns null for:
    ///   - Invalid signature (token modified)
    ///   - Expired token (past expiration time)
    ///   - Wrong issuer/audience (token from/for different app)
    ///   - Malformed token (not valid JWT format)
    ///   - Any exception during validation
    /// 
    /// Called automatically by JWT authentication middleware!
    /// </remarks>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        // Try-catch block: Handle exceptions gracefully
        // If any exception occurs, return null (invalid token)
        try
        {
            // JwtSecurityTokenHandler reads and validates JWT tokens
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Convert secret string to bytes (same as GenerateToken)
            var key = Encoding.UTF8.GetBytes(_secret);

            // Define validation parameters (what to check)
            var validationParameters = new TokenValidationParameters
            {
                // Signature validation
                ValidateIssuerSigningKey = true,  // Must verify signature
                IssuerSigningKey = new SymmetricSecurityKey(key),  // Key to verify with
                
                // Issuer validation (who created token)
                ValidateIssuer = true,    // Must check issuer
                ValidIssuer = _issuer,    // Expected issuer value
                
                // Audience validation (who should use token)
                ValidateAudience = true,  // Must check audience
                ValidAudience = _audience,  // Expected audience value
                
                // Lifetime validation (token not expired)
                ValidateLifetime = true,  // Must check expiration
                ClockSkew = TimeSpan.Zero  // No grace period for expiration
            };
            
            // ClockSkew explained:
            //   Default ClockSkew = 5 minutes (accounts for clock differences)
            //   Setting to Zero means: expired = immediately invalid
            //   Example with default ClockSkew:
            //     Token expires: 14:00:00
            //     Still valid until: 14:05:00 (5 min grace period)
            //   With ClockSkew = Zero:
            //     Token expires: 14:00:00
            //     Invalid at: 14:00:01 (no grace period)

            // ValidateToken() performs all validation checks
            // Parameters:
            //   - token: JWT string to validate
            //   - validationParameters: What to check
            //   - out SecurityToken: The validated token object (not used here)
            // 
            // 'out' keyword in C#:
            //   Method can return multiple values
            //   Main return value: ClaimsPrincipal
            //   Out parameter: SecurityToken (we ignore it with '_')
            //   The '_' discard pattern means "I don't care about this value"
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            
            // If validation succeeds, principal contains user claims
            // If validation fails, exception is thrown (caught below)
            return principal;
        }
        catch
        {
            // Any exception means invalid token
            // Exceptions can be:
            //   - SecurityTokenExpiredException: Token expired
            //   - SecurityTokenInvalidSignatureException: Signature mismatch
            //   - SecurityTokenInvalidIssuerException: Wrong issuer
            //   - ArgumentException: Malformed token
            //   - etc.
            // 
            // We catch all exceptions and return null (token is invalid)
            // In production, you might want to log the specific exception
            return null;
        }
    }
}
