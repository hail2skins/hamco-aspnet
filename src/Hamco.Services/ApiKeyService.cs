using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using BCrypt.Net;

namespace Hamco.Services;

/// <summary>
/// Service for managing API key authentication.
/// Handles generation, validation, and revocation of API keys.
/// </summary>
/// <remarks>
/// Implementation Details:
///   - Uses BCrypt for key hashing (same as password hashing)
///   - Generates cryptographically secure random keys
///   - Key format: hamco_sk_{random} (32+ chars of randomness)
///   - Integrates with existing JWT auth (both work simultaneously)
///   - Sets ClaimsPrincipal for ASP.NET Core authorization
/// 
/// Security Considerations:
///   🔐 Plaintext keys never stored (hashed with BCrypt)
///   🔐 Keys returned only once (on generation)
///   🔐 Expired/revoked keys fail immediately
///   🔐 Constant-time comparison (BCrypt handles this)
///   🔐 Cryptographically secure random number generator
/// 
/// Performance Notes:
///   ⚠️ BCrypt is slow by design (~100ms per hash)
///   ⚠️ Validating against N keys = N BCrypt operations worst case
///   Future optimizations:
///     - Prefix-based quick lookup (index on KeyPrefix)
///     - Caching validated keys (short TTL)
///     - Check expiry/active before BCrypt (fast fail)
/// 
/// Thread Safety:
///   DbContext is NOT thread-safe!
///   Each HTTP request gets new DbContext (scoped lifetime).
///   This service is transient/scoped (safe with scoped DbContext).
/// </remarks>
public class ApiKeyService : IApiKeyService
{
    private readonly HamcoDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ApiKeyService.
    /// </summary>
    /// <param name="context">Database context for API key persistence.</param>
    /// <remarks>
    /// Dependency Injection:
    ///   ASP.NET Core DI container provides HamcoDbContext.
    ///   Context is scoped (one per HTTP request).
    ///   Service should also be scoped (or transient).
    /// 
    /// Registration (Program.cs):
    ///   services.AddScoped&lt;IApiKeyService, ApiKeyService&gt;();
    /// </remarks>
    public ApiKeyService(HamcoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Generates a new API key with specified permissions.
    /// Returns plaintext key ONCE - caller must save securely!
    /// </summary>
    /// <param name="name">Human-readable key name (e.g., "Production Bot").</param>
    /// <param name="isAdmin">Whether key has Admin role (write access).</param>
    /// <param name="createdByUserId">User ID of admin creating the key.</param>
    /// <returns>Tuple of plaintext key and ApiKey entity.</returns>
    /// <exception cref="ArgumentNullException">If name is null.</exception>
    /// <exception cref="ArgumentException">If name is empty/whitespace.</exception>
    public async Task<(string key, ApiKey entity)> GenerateKeyAsync(
        string name, 
        bool isAdmin, 
        string createdByUserId)
    {
        // Validate inputs
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty or whitespace.", nameof(name));

        // Generate cryptographically secure random key
        // Format: hamco_sk_{32 chars of hex}
        var randomBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var randomPart = Convert.ToHexString(randomBytes).ToLowerInvariant(); // Lowercase hex
        var plaintextKey = $"hamco_sk_{randomPart}";

        // Extract prefix (first 8 chars for display)
        var prefix = plaintextKey[..Math.Min(8, plaintextKey.Length)];

        // Hash the key with BCrypt (never store plaintext!)
        var keyHash = BCrypt.Net.BCrypt.HashPassword(plaintextKey);

        // Create entity
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = prefix,
            IsAdmin = isAdmin,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            ExpiresAt = null // Default: never expires
        };

        // Persist to database
        await _context.Set<ApiKey>().AddAsync(apiKey);
        await _context.SaveChangesAsync();

        // Return plaintext key + entity
        // ⚠️ This is the ONLY time plaintext key is ever returned!
        return (plaintextKey, apiKey);
    }

    /// <summary>
    /// Validates an API key and returns ClaimsPrincipal if valid.
    /// Returns null if key is invalid, expired, or revoked.
    /// </summary>
    /// <param name="apiKey">Plaintext API key to validate.</param>
    /// <returns>ClaimsPrincipal if valid, null otherwise.</returns>
    /// <remarks>
    /// Validation Steps:
    ///   1. Check format (starts with hamco_sk_, minimum length)
    ///   2. Load active keys from database (IsActive = true)
    ///   3. For each key:
    ///      a. Check expiry (fast fail if expired)
    ///      b. BCrypt.Verify(apiKey, KeyHash)
    ///      c. If match, create ClaimsPrincipal
    ///   4. Return principal or null
    /// 
    /// Performance:
    ///   Worst case: O(N) where N = number of active keys
    ///   BCrypt adds ~100ms per key verification
    ///   Optimizations applied:
    ///     - Format check first (fast fail)
    ///     - Expiry check before BCrypt (fast fail)
    ///     - Only load active keys (skip revoked)
    /// </remarks>
    public async Task<ClaimsPrincipal?> ValidateKeyAsync(string apiKey)
    {
        // Fast fail: Check format
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;
        if (!apiKey.StartsWith("hamco_sk_"))
            return null;
        if (apiKey.Length < 40) // hamco_sk_ (9) + reasonable randomness (31+)
            return null;

        // Load all active API keys
        // Future optimization: Filter by KeyPrefix for faster lookup
        var activeKeys = await _context.Set<ApiKey>()
            .Where(k => k.IsActive)
            .ToListAsync();

        // Try to match against each active key
        foreach (var storedKey in activeKeys)
        {
            // Fast fail: Check expiry before expensive BCrypt verification
            if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
                continue; // Expired, skip

            // Verify key hash (expensive operation!)
            if (BCrypt.Net.BCrypt.Verify(apiKey, storedKey.KeyHash))
            {
                // Match found! Create ClaimsPrincipal
                return CreateClaimsPrincipal(storedKey);
            }
        }

        // No match found
        return null;
    }

    /// <summary>
    /// Revokes an API key by setting IsActive = false.
    /// </summary>
    /// <param name="keyId">Unique identifier of the key to revoke.</param>
    /// <exception cref="InvalidOperationException">If key not found.</exception>
    /// <remarks>
    /// Soft Delete Pattern:
    ///   - Don't delete from database (preserve audit trail)
    ///   - Set IsActive = false (disables authentication)
    ///   - Can reactivate later if needed
    /// 
    /// Idempotency:
    ///   Revoking already-revoked key succeeds (no error).
    ///   Only throws if key doesn't exist at all.
    /// 
    /// Immediate Effect:
    ///   Revoked keys fail validation immediately.
    ///   No grace period or delayed revocation.
    /// </remarks>
    public async Task RevokeKeyAsync(string keyId)
    {
        // Find key by ID
        var apiKey = await _context.Set<ApiKey>().FindAsync(keyId);
        
        if (apiKey == null)
        {
            throw new InvalidOperationException($"API key with ID '{keyId}' not found.");
        }

        // Set inactive (soft delete)
        apiKey.IsActive = false;
        
        // Persist change
        _context.Update(apiKey);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a ClaimsPrincipal from a validated API key.
    /// </summary>
    /// <param name="apiKey">The validated API key entity.</param>
    /// <returns>ClaimsPrincipal with appropriate claims and role.</returns>
    /// <remarks>
    /// Claims Structure:
    ///   - NameIdentifier: ApiKey.Id (unique identifier)
    ///   - Email: "apikey:{Name}" (human-readable, for logs)
    ///   - Role: "Admin" or "User" (for authorization)
    ///   - Custom: "api_key_id", "api_key_name", "auth_method" (for analytics)
    /// 
    /// Authentication Type:
    ///   "ApiKey" - distinguishes from "Bearer" (JWT) auth
    ///   Important for User.Identity.IsAuthenticated
    /// 
    /// Integration with [Authorize]:
    ///   [Authorize] checks User.Identity.IsAuthenticated
    ///   [Authorize(Roles="Admin")] checks User.IsInRole("Admin")
    ///   Works seamlessly with existing controllers!
    /// </remarks>
    private ClaimsPrincipal CreateClaimsPrincipal(ApiKey apiKey)
    {
        var claims = new List<Claim>
        {
            // Standard claims (ASP.NET Core convention)
            new Claim(ClaimTypes.NameIdentifier, apiKey.Id),
            new Claim(ClaimTypes.Email, $"apikey:{apiKey.Name}"),
            new Claim(ClaimTypes.Role, apiKey.IsAdmin ? "Admin" : "User"),
            
            // Custom claims (for analytics and debugging)
            new Claim("api_key_id", apiKey.Id),
            new Claim("api_key_name", apiKey.Name),
            new Claim("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        return new ClaimsPrincipal(identity);
    }
}
