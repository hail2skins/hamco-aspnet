using Hamco.Core.Models;
using System.Security.Claims;

namespace Hamco.Core.Services;

/// <summary>
/// Interface for API Key management and validation operations.
/// Handles generation, validation, and revocation of API keys for external agents.
/// </summary>
/// <remarks>
/// API Key Authentication Flow:
///   1. Admin generates key via POST /api/admin/api-keys → GenerateKeyAsync()
///   2. Service returns plaintext key ONCE (client must save it!)
///   3. External service includes X-API-Key header in requests
///   4. Middleware calls ValidateKeyAsync() for each request
///   5. If valid, HttpContext.User set with appropriate role claims
///   6. Existing [Authorize] attributes work unchanged
/// 
/// Why use an interface?
///   - Testability: Mock IApiKeyService in unit tests
///   - Flexibility: Swap implementations (different hashing, caching)
///   - Dependency Injection: ASP.NET Core standard pattern
///   - Separation of concerns: Interface in Core, implementation in Services
/// 
/// Security Principles:
///   🔐 Never store plaintext keys (hash with BCrypt)
///   🔐 Return plaintext key only once (on generation)
///   🔐 Validate expiry before checking hash (performance + security)
///   🔐 Check IsActive flag (revoked keys fail fast)
///   🔐 Use cryptographically secure random generator
/// 
/// Comparison with JWT:
///   API Keys:
///     - Stored in database (can query, revoke, audit)
///     - Long-lived (don't expire frequently)
///     - Per-service identity (not per-user)
///     - Revocable instantly (set IsActive=false)
///   
///   JWT:
///     - Not stored (stateless, validated by signature)
///     - Short-lived (expire after hours/days)
///     - Per-user identity (tied to User entity)
///     - Can't revoke individual tokens (logout = ignore token)
/// 
/// Performance Considerations:
///   - BCrypt is slow by design (~100ms per hash)
///   - For high-traffic APIs, consider:
///     * Caching validated keys (short TTL)
///     * Prefix-based quick lookup before BCrypt
///     * Rate limiting per API key
/// 
/// Future Enhancements:
///   - Scoped permissions (notes:read, notes:write)
///   - Usage tracking (LastUsedAt, RequestCount)
///   - Rate limiting per key
///   - IP allowlisting per key
///   - Automatic rotation (generate new, deprecate old)
/// </remarks>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key with the specified permissions.
    /// Returns the plaintext key ONCE - client must save it securely!
    /// </summary>
    /// <param name="name">Human-readable name for the key (e.g., "Production Bot").</param>
    /// <param name="isAdmin">Whether the key has Admin privileges (write access).</param>
    /// <param name="createdByUserId">User ID of the admin creating the key.</param>
    /// <returns>
    /// Tuple containing:
    ///   - key: Plaintext API key (hamco_sk_...) - ONLY time it's ever returned!
    ///   - entity: ApiKey entity with Id, Name, KeyPrefix, etc. (for display)
    /// </returns>
    /// <remarks>
    /// Key Generation Process:
    ///   1. Generate cryptographically secure random bytes
    ///   2. Encode as base62 or hex (lowercase alphanumeric)
    ///   3. Prepend "hamco_sk_" prefix
    ///   4. Extract first 8 chars for KeyPrefix
    ///   5. Hash full key with BCrypt → KeyHash
    ///   6. Create ApiKey entity (Id, Name, KeyHash, KeyPrefix, etc.)
    ///   7. Save to database
    ///   8. Return plaintext key + entity
    /// 
    /// Key Format: hamco_sk_{32+ random chars}
    ///   Example: hamco_sk_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
    /// 
    /// Why return tuple?
    ///   - Need plaintext key for client
    ///   - Need entity for metadata (Id for revocation, Name for display)
    ///   - Alternative: Create response DTO (cleaner but more code)
    /// 
    /// Security:
    ///   ⚠️ Plaintext key never stored or logged!
    ///   ⚠️ Returned exactly once (like password reset token)
    ///   ⚠️ Client responsible for secure storage
    ///   ⚠️ If lost, must generate new key (can't recover)
    /// 
    /// Usage example (Controller):
    ///   var (key, apiKey) = await _apiKeyService.GenerateKeyAsync(
    ///       name: "Production Deploy Bot",
    ///       isAdmin: true,
    ///       createdByUserId: User.FindFirst(ClaimTypes.NameIdentifier)!.Value
    ///   );
    ///   
    ///   return Ok(new {
    ///       key = key,  // ⚠️ Show once, never again!
    ///       id = apiKey.Id,
    ///       name = apiKey.Name,
    ///       prefix = apiKey.KeyPrefix,
    ///       message = "Save this key securely. You won't see it again!"
    ///   });
    /// 
    /// Why async?
    ///   - Database save operation (I/O bound)
    ///   - BCrypt hashing (CPU bound but can be async)
    ///   - Follows ASP.NET Core async best practices
    /// 
    /// Error handling:
    ///   - Throws if database save fails
    ///   - Throws if createdByUserId invalid (optional validation)
    ///   - Returns valid key or throws (no null returns)
    /// </remarks>
    Task<(string key, ApiKey entity)> GenerateKeyAsync(
        string name, 
        bool isAdmin, 
        string createdByUserId);

    /// <summary>
    /// Validates an API key and returns a ClaimsPrincipal for authentication.
    /// Returns null if key is invalid, expired, or revoked.
    /// </summary>
    /// <param name="apiKey">The plaintext API key to validate (from X-API-Key header).</param>
    /// <returns>
    /// ClaimsPrincipal with user identity and role claims if valid,
    /// null if invalid/expired/revoked.
    /// </returns>
    /// <remarks>
    /// Validation Process:
    ///   1. Check key format (starts with "hamco_sk_", minimum length)
    ///   2. Query database for active keys (IsActive = true)
    ///   3. For each active key:
    ///      a. Check expiry (if ExpiresAt set and passed, skip)
    ///      b. BCrypt.Verify(apiKey, KeyHash)
    ///      c. If match, create ClaimsPrincipal
    ///   4. Return principal or null
    /// 
    /// ClaimsPrincipal Structure:
    ///   new ClaimsPrincipal(new ClaimsIdentity(new[] {
    ///       new Claim(ClaimTypes.NameIdentifier, apiKey.Id),
    ///       new Claim(ClaimTypes.Email, $"apikey:{apiKey.Name}"),
    ///       new Claim(ClaimTypes.Role, apiKey.IsAdmin ? "Admin" : "User"),
    ///       new Claim("api_key_id", apiKey.Id),
    ///       new Claim("auth_method", "api_key")
    ///   }, "ApiKey"));
    /// 
    /// Why these claims?
    ///   - NameIdentifier: Used by [Authorize] (identifies principal)
    ///   - Email: Human-readable identifier (logs, audit)
    ///   - Role: "Admin" or "User" (for [Authorize(Roles="Admin")])
    ///   - api_key_id: Track which key was used (analytics)
    ///   - auth_method: Distinguish API key vs JWT auth
    /// 
    /// Authentication Scheme:
    ///   ClaimsIdentity("ApiKey") sets the authentication type.
    ///   Important for User.Identity.IsAuthenticated to return true!
    /// 
    /// Integration with Existing Auth:
    ///   Controllers using [Authorize(Roles="Admin")] work unchanged!
    ///   Middleware sets HttpContext.User = principal
    ///   ASP.NET Core authorization checks User.IsInRole("Admin")
    ///   API key with IsAdmin=true → passes check ✅
    ///   API key with IsAdmin=false → fails check ❌ (403 Forbidden)
    /// 
    /// Performance:
    ///   ⚠️ BCrypt is slow (~100ms per key)
    ///   For 10 active keys, worst case = 1 second validation
    ///   Optimizations:
    ///     - Check format first (fast fail for invalid format)
    ///     - Check expiry before BCrypt (fast fail for expired)
    ///     - Consider prefix-based lookup (index on KeyPrefix)
    ///     - Cache validated keys (5-minute TTL, invalidate on revoke)
    /// 
    /// Security:
    ///   ✅ Constant-time comparison (BCrypt handles this)
    ///   ✅ Rate limiting (implement in middleware, not here)
    ///   ✅ Audit logging (log failed attempts)
    ///   ⚠️ Never log the actual API key! Log prefix only.
    /// 
    /// Usage example (Middleware):
    ///   var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
    ///   if (!string.IsNullOrEmpty(apiKey))
    ///   {
    ///       var principal = await _apiKeyService.ValidateKeyAsync(apiKey);
    ///       if (principal != null)
    ///       {
    ///           context.User = principal;
    ///       }
    ///       else
    ///       {
    ///           // Invalid key - log attempt (with prefix only!)
    ///           _logger.LogWarning("Invalid API key: {Prefix}", 
    ///               apiKey.Length >= 8 ? apiKey[..8] : "invalid");
    ///       }
    ///   }
    /// 
    /// Why return null instead of throwing?
    ///   - Invalid key is not exceptional (happens frequently)
    ///   - Middleware handles null (continues to next auth method)
    ///   - Throwing would pollute error logs
    ///   - Null = "not authenticated" (try next method or 401)
    /// </remarks>
    Task<ClaimsPrincipal?> ValidateKeyAsync(string apiKey);

    /// <summary>
    /// Revokes an API key by setting IsActive = false.
    /// Revoked keys cannot be used for authentication.
    /// </summary>
    /// <param name="keyId">The unique identifier of the API key to revoke.</param>
    /// <returns>Task representing the async revocation operation.</returns>
    /// <remarks>
    /// Soft Delete Pattern:
    ///   - Don't DELETE FROM database (lose audit trail)
    ///   - UPDATE is_active = false (preserve history)
    ///   - Can reactivate if needed (accidental revocation)
    /// 
    /// Revocation Process:
    ///   1. Find ApiKey by Id
    ///   2. If not found, throw or return (design choice)
    ///   3. Set IsActive = false
    ///   4. Save to database
    ///   5. Invalidate cache (if caching validated keys)
    /// 
    /// Immediate Effect:
    ///   Revoked keys fail validation immediately.
    ///   No "grace period" or delayed effect.
    ///   
    ///   Why?
    ///     If key compromised, need instant revocation!
    ///     Can't wait for cache TTL or next sync.
    /// 
    /// Cache Invalidation:
    ///   If caching validated keys, must invalidate on revoke!
    ///   Options:
    ///     - Clear entire cache (simple but overkill)
    ///     - Remove specific key from cache (efficient)
    ///     - Use cache dependencies (automatic invalidation)
    /// 
    /// Error Handling:
    ///   - Key not found: Throw KeyNotFoundException? Return silently?
    ///   - Design choice depends on API contract
    ///   - Idempotent delete: Revoking already-revoked key succeeds
    /// 
    /// Usage example (Controller):
    ///   [HttpDelete("{id}")]
    ///   public async Task&lt;IActionResult&gt; RevokeKey(string id)
    ///   {
    ///       await _apiKeyService.RevokeKeyAsync(id);
    ///       return NoContent();  // 204 No Content
    ///   }
    /// 
    /// Authorization:
    ///   Only admins should revoke keys!
    ///   Controller should have [Authorize(Roles="Admin")]
    ///   
    ///   Additional check (optional):
    ///     Only allow revoking keys you created?
    ///     Or any admin can revoke any key?
    ///     Security vs flexibility trade-off.
    /// 
    /// Audit Logging:
    ///   Log revocation events:
    ///     "API key {Id} ({Name}) revoked by user {UserId}"
    ///   Helps with:
    ///     - Security investigations
    ///     - Accountability
    ///     - Compliance (who did what when)
    /// 
    /// Hard Delete:
    ///   Generally not recommended (lose audit trail).
    ///   Only if:
    ///     - Regulatory requirement (GDPR right to be forgotten)
    ///     - Key + hash both compromised (defense in depth)
    ///   
    ///   If needed, add separate PermanentlyDeleteKeyAsync() method.
    /// 
    /// Why async?
    ///   Database operation (I/O bound).
    ///   Follows ASP.NET Core async best practices.
    ///   Allows concurrent revocations without blocking.
    /// </remarks>
    Task RevokeKeyAsync(string keyId);
}
