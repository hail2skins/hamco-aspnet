namespace Hamco.Core.Models;

/// <summary>
/// Represents an API key for authenticating external agents and applications.
/// Provides stateless authentication for automated systems (CI/CD, bots, integrations).
/// </summary>
/// <remarks>
/// What is an API Key?
///   An API key is a secret token that identifies and authenticates an application
///   or service (not a human user). Common in REST APIs for:
///   - CI/CD pipelines posting deployment notifications
///   - Bots (like Skippy) posting automated notes
///   - Third-party integrations
///   - Mobile apps (though JWT often preferred)
/// 
/// API Keys vs JWT:
///   API Keys:
///     ✅ Long-lived (don't expire frequently)
///     ✅ Simpler (just a header, no login flow)
///     ✅ Per-service (easy to revoke individual keys)
///     ❌ No user context (just app identity)
///     ❌ If leaked, valid until revoked
///   
///   JWT:
///     ✅ Short-lived (expires after hours/days)
///     ✅ User context (tied to specific user)
///     ✅ Self-contained (includes user info)
///     ❌ Can't revoke single token (logout = new token)
///     ❌ Requires login flow (username/password)
/// 
/// Security Model:
///   1. Admin generates API key via admin panel
///   2. Key returned ONCE (like a password reset token)
///   3. Key hash stored in database (never plaintext!)
///   4. External service uses key in X-API-Key header
///   5. Middleware validates key, sets HttpContext.User
///   6. Existing [Authorize(Roles="Admin")] attributes work unchanged
/// 
/// Key Format: hamco_sk_{random}
///   - hamco: Application prefix (identifies source)
///   - sk: Secret Key (type identifier)
///   - {random}: 32+ random characters (cryptographically secure)
///   
///   Example: hamco_sk_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0
/// 
/// Why this format?
///   ✅ Searchable in code/logs (find all Hamco keys)
///   ✅ Type-safe (sk vs pk for public keys)
///   ✅ Industry standard (GitHub: ghp_, Stripe: sk_, etc.)
///   ✅ Easy to validate (regex: ^hamco_sk_[a-z0-9]{32,}$)
/// 
/// Hashing Strategy (BCrypt):
///   - Same approach as password hashing
///   - BCrypt.HashPassword(key) → store in KeyHash
///   - BCrypt.Verify(providedKey, KeyHash) → validate
///   - Even if database leaked, keys can't be recovered
/// 
/// Prefix Storage:
///   - Store first 8 chars for UI display
///   - Example: "hamco_sk" or "hamco_sk_a1b2c3d4"
///   - Helps admins identify keys without exposing full secret
///   - UI shows: "hamco_sk_a1b2c3d4... (Production Bot)"
/// 
/// Admin vs Read-Only Keys:
///   - IsAdmin = true: Can POST/PUT/DELETE (like Admin role)
///   - IsAdmin = false: Can only GET (read-only access)
///   - Future: More granular permissions (scopes: notes:read, notes:write)
/// 
/// Expiration:
///   - ExpiresAt = null: Never expires (until revoked)
///   - ExpiresAt = DateTime: Auto-expires (for temporary access)
///   - Middleware checks expiry before validating
/// 
/// Revocation:
///   - IsActive = false: Key disabled (soft delete)
///   - Better than hard delete (audit trail preserved)
///   - Can re-enable later if needed
/// 
/// Audit Trail:
///   - CreatedAt: When key was generated
///   - CreatedByUserId: Which admin created it
///   - Name: Human-readable label ("Production Deploy Bot")
///   - Helps with compliance, debugging, and security audits
/// 
/// Integration with Existing Auth:
///   Middleware sets HttpContext.User with ClaimsPrincipal:
///   - NameIdentifier claim: ApiKey.Id (not a user ID!)
///   - Email claim: "apikey:{Name}" (identifies which key)
///   - Role claim: "Admin" (if IsAdmin=true) or "User"
///   
///   Existing controllers work unchanged:
///     [Authorize(Roles="Admin")] // Works with both JWT and API keys!
/// 
/// Example Usage:
///   POST /api/notes
///   Headers:
///     X-API-Key: hamco_sk_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
///     Content-Type: application/json
///   Body:
///     { "title": "Deployed v1.2.3", "content": "..." }
/// </remarks>
public class ApiKey
{
    /// <summary>
    /// Unique identifier for the API key.
    /// </summary>
    /// <remarks>
    /// Primary key in database (GUID as string).
    /// Not exposed to external clients (they use the actual key).
    /// Used internally for revocation, tracking, etc.
    /// 
    /// Why string instead of Guid type?
    ///   - PostgreSQL uuid type supported
    ///   - But strings more flexible (easier to work with in APIs)
    ///   - GUID format validation happens in service layer
    /// </remarks>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable name for the API key.
    /// Helps identify which service/bot is using it.
    /// </summary>
    /// <remarks>
    /// Examples:
    ///   - "Production Deploy Bot"
    ///   - "Skippy Note Agent"
    ///   - "CI/CD Pipeline"
    ///   - "Mobile App (iOS)"
    /// 
    /// Required field (empty string default for EF Core).
    /// Admins should always provide a meaningful name.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hash of the API key.
    /// Never store plaintext keys!
    /// </summary>
    /// <remarks>
    /// Hashing process:
    ///   1. Generate random key: hamco_sk_a1b2c3d4...
    ///   2. Hash with BCrypt: $2a$11$...
    ///   3. Store hash in KeyHash column
    ///   4. Return plaintext key to admin (ONCE!)
    ///   5. Admin saves key securely
    /// 
    /// Validation process:
    ///   1. Client sends X-API-Key: hamco_sk_a1b2c3d4...
    ///   2. Middleware extracts key from header
    ///   3. Query database for all active keys
    ///   4. BCrypt.Verify(providedKey, KeyHash) for each
    ///   5. If match found, authenticate request
    /// 
    /// Why BCrypt?
    ///   - Same as password hashing (proven secure)
    ///   - Adaptive (can increase cost as hardware improves)
    ///   - Salted automatically (no rainbow table attacks)
    ///   - Slow by design (prevents brute force)
    /// 
    /// Performance concern:
    ///   BCrypt is intentionally slow (~100ms per hash).
    ///   For API keys, consider indexing or caching strategies.
    ///   Could extract KeyPrefix for quick lookup before BCrypt verify.
    /// </remarks>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the API key for display purposes.
    /// Helps identify keys in UI without exposing full secret.
    /// </summary>
    /// <remarks>
    /// Example: "hamco_sk" or "hamco_sk_a1b2c3d4"
    /// 
    /// Use cases:
    ///   - Admin dashboard: List all keys with prefixes
    ///   - Audit logs: Show which key was used
    ///   - Revocation UI: "Are you sure you want to revoke hamco_sk_a1b2c3d4...?"
    /// 
    /// Security consideration:
    ///   8 chars is safe to expose (too short to guess remaining chars).
    ///   Full key is 32+ chars, so 24+ chars remain secret.
    ///   2^(24*5) = 2^120 possible combinations (brute force infeasible).
    /// </remarks>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Whether this key has Admin privileges.
    /// </summary>
    /// <remarks>
    /// IsAdmin = true:
    ///   - Can POST, PUT, DELETE (write operations)
    ///   - Added to "Admin" role in ClaimsPrincipal
    ///   - Works with [Authorize(Roles="Admin")] attributes
    /// 
    /// IsAdmin = false:
    ///   - Can only GET (read operations)
    ///   - Added to "User" role in ClaimsPrincipal
    ///   - Fails [Authorize(Roles="Admin")] checks (403 Forbidden)
    /// 
    /// Future enhancement:
    ///   Replace boolean with scopes/permissions:
    ///   - Scopes: ["notes:read", "notes:write", "users:read"]
    ///   - More granular than just Admin/User
    ///   - Industry standard (OAuth 2.0 scopes)
    /// </remarks>
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// Optional expiration date/time for the API key.
    /// Null means never expires.
    /// </summary>
    /// <remarks>
    /// Use cases for expiring keys:
    ///   - Temporary contractor access (expires after project)
    ///   - Demo/trial periods (expires after 30 days)
    ///   - Scheduled key rotation (security best practice)
    /// 
    /// Validation:
    ///   if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
    ///       return null; // Key expired
    /// 
    /// Why DateTime? not DateOnly?
    ///   - Need precision (expire at 23:59:59, not just date)
    ///   - DateOnly new in .NET 6 (DateTime more compatible)
    /// 
    /// Why UTC?
    ///   - Server might run in different timezone
    ///   - UTC is unambiguous (no DST issues)
    ///   - Always use DateTime.UtcNow for consistency
    /// </remarks>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the API key was created.
    /// </summary>
    /// <remarks>
    /// Automatically set to current UTC time on creation.
    /// Used for:
    ///   - Audit logs (when was key generated?)
    ///   - Security reviews (old keys should be rotated)
    ///   - Sorting keys by creation date
    /// 
    /// Database default:
    ///   OnModelCreating: .HasDefaultValueSql("CURRENT_TIMESTAMP")
    ///   Ensures CreatedAt set even if not provided by code.
    /// </remarks>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the admin who created this API key.
    /// </summary>
    /// <remarks>
    /// Foreign key to Users table (not enforced at DB level).
    /// 
    /// Why not enforced?
    ///   - API keys can outlive users (user deleted, key remains)
    ///   - Empty string if created by system/migration
    ///   - Could be nullable Guid instead
    /// 
    /// Use cases:
    ///   - Audit trail (who created this key?)
    ///   - List "my keys" (keys I created)
    ///   - Accountability (if key misused, know who created it)
    /// 
    /// Future enhancement:
    ///   Add LastUsedAt, LastUsedFrom (IP address) for security monitoring.
    /// </remarks>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the API key is active.
    /// Inactive keys cannot be used for authentication.
    /// </summary>
    /// <remarks>
    /// Soft delete pattern:
    ///   - Instead of DELETE FROM api_keys WHERE id = ...
    ///   - UPDATE api_keys SET is_active = false WHERE id = ...
    /// 
    /// Benefits:
    ///   ✅ Audit trail preserved (can see what keys existed)
    ///   ✅ Can reactivate if needed (accidental revocation)
    ///   ✅ Analytics (how many keys created/revoked over time)
    /// 
    /// Validation:
    ///   Middleware checks IsActive before validating hash.
    ///   Inactive keys fail immediately (no BCrypt overhead).
    /// 
    /// Hard delete use case:
    ///   If key compromised AND hash leaked (defense in depth).
    ///   But normally, soft delete is sufficient.
    /// </remarks>
    public bool IsActive { get; set; } = true;
}
