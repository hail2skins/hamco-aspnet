using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;

namespace Hamco.Api.Controllers.Admin;

/// <summary>
/// API controller for managing API keys (admin-only operations).
/// Handles generation, listing, and revocation of API keys.
/// </summary>
/// <remarks>
/// REST API endpoints:
///   POST   /api/admin/api-keys       - Generate new API key (returns plaintext ONCE!)
///   GET    /api/admin/api-keys       - List all API keys (without plaintext keys)
///   DELETE /api/admin/api-keys/{id}  - Revoke API key (soft delete)
/// 
/// Authorization:
///   ALL endpoints require [Authorize(Roles = "Admin")]!
///   Only admins can manage API keys.
///   
///   Why?
///     - API keys can have Admin role (write access to notes)
///     - Generating admin keys = escalating privilege
///     - Must be restricted to trusted admins only
/// 
/// API Key Lifecycle:
///   1. Admin generates key (POST)
///      → Returns plaintext key ONCE
///      → Client must save key securely
///      → Server stores BCrypt hash only
///   
///   2. External service uses key
///      → Include X-API-Key header in requests
///      → Middleware validates key
///      → Sets HttpContext.User with role claims
///      → Existing [Authorize] attributes work unchanged
///   
///   3. Admin revokes key (DELETE)
///      → Sets IsActive = false (soft delete)
///      → Key immediately stops working
///      → Audit trail preserved
/// 
/// Security Considerations:
///   🔐 Plaintext key returned ONLY on generation (POST)
///   🔐 List endpoint returns prefix only (not full key or hash)
///   🔐 Keys hashed with BCrypt (same as passwords)
///   🔐 Admin-only operations (no regular users)
///   🔐 Created by user tracked for accountability
/// 
/// Response DTOs:
///   We return anonymous objects (new { ... }) for simplicity.
///   Production apps might use formal DTO classes.
///   
///   Why anonymous objects here?
///     - Simpler (no extra files)
///     - Type-safe (C# compiler checks)
///     - JSON serialization automatic (ASP.NET Core)
///     - Good enough for this API size
/// 
/// Example Usage:
///   # Generate key
///   curl -X POST https://hamco.app/api/admin/api-keys \
///     -H "Authorization: Bearer {admin_jwt}" \
///     -H "Content-Type: application/json" \
///     -d '{"name":"Production Bot","isAdmin":true}'
///   
///   Response:
///   {
///     "key": "hamco_sk_a1b2c3d4e5f6...",  // ⚠️ Save this! Won't see again!
///     "id": "guid...",
///     "name": "Production Bot",
///     "prefix": "hamco_sk",
///     "message": "Save this key securely. You won't see it again!"
///   }
///   
///   # Use key
///   curl -X POST https://hamco.app/api/notes \
///     -H "X-API-Key: hamco_sk_a1b2c3d4e5f6..." \
///     -H "Content-Type: application/json" \
///     -d '{"title":"New Note","content":"..."}'
/// </remarks>
[ApiController]
[Route("api/admin/api-keys")]
[Authorize(Roles = "Admin")]  // 🔒 Admin-only controller!
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly HamcoDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ApiKeysController.
    /// </summary>
    /// <param name="apiKeyService">Service for API key operations.</param>
    /// <param name="context">Database context for querying API keys.</param>
    public ApiKeysController(IApiKeyService apiKeyService, HamcoDbContext context)
    {
        _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Generates a new API key.
    /// </summary>
    /// <param name="request">API key generation request.</param>
    /// <returns>
    /// 201 Created with API key details (including plaintext key - ONLY TIME it's returned!).
    /// 400 Bad Request if validation fails.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if not admin.
    /// </returns>
    /// <remarks>
    /// Request body:
    /// {
    ///   "name": "Production Bot",  // Human-readable identifier
    ///   "isAdmin": true            // Admin role = write access
    /// }
    /// 
    /// Response (201 Created):
    /// {
    ///   "key": "hamco_sk_a1b2c3d4...",  // ⚠️ Plaintext key - save it!
    ///   "id": "guid...",
    ///   "name": "Production Bot",
    ///   "prefix": "hamco_sk_a1b2c3d4",
    ///   "isAdmin": true,
    ///   "createdAt": "2024-02-07T12:34:56Z",
    ///   "message": "Save this key securely. You won't see it again!"
    /// }
    /// 
    /// ⚠️ WARNING: Plaintext key returned ONLY ONCE!
    ///   Client MUST save it securely (environment variable, secret manager).
    ///   If lost, must generate new key (can't recover old one).
    /// 
    /// Security:
    ///   - Key hash stored in database (never plaintext)
    ///   - CreatedByUserId tracks which admin created it
    ///   - Admin can create admin keys (privilege escalation risk)
    ///     → Only trust admins with this endpoint!
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateApiKey([FromBody] GenerateApiKeyRequest request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        // Get current user ID (from JWT claims)
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User ID not found in token." });
        }

        // Generate key
        var (key, entity) = await _apiKeyService.GenerateKeyAsync(
            request.Name,
            request.IsAdmin,
            userId
        );

        // Return 201 Created with key details
        return CreatedAtAction(
            nameof(GetApiKey),
            new { id = entity.Id },
            new
            {
                key = key,  // ⚠️ Plaintext key - ONLY time it's returned!
                id = entity.Id,
                name = entity.Name,
                prefix = entity.KeyPrefix,
                isAdmin = entity.IsAdmin,
                createdAt = entity.CreatedAt,
                message = "Save this key securely. You won't see it again!"
            }
        );
    }

    /// <summary>
    /// Lists all API keys created by the current user.
    /// </summary>
    /// <returns>
    /// 200 OK with array of API key summaries.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if not admin.
    /// </returns>
    /// <remarks>
    /// Response (200 OK):
    /// [
    ///   {
    ///     "id": "guid...",
    ///     "name": "Production Bot",
    ///     "prefix": "hamco_sk_a1b2c3d4",  // First 8 chars only!
    ///     "isAdmin": true,
    ///     "isActive": true,
    ///     "createdAt": "2024-02-07T12:34:56Z",
    ///     "expiresAt": null
    ///   },
    ///   ...
    /// ]
    /// 
    /// Security:
    ///   ❌ Does NOT return full key (lost = must generate new one)
    ///   ❌ Does NOT return KeyHash (sensitive, like password hash)
    ///   ✅ Returns prefix for identification
    ///   ✅ Returns IsActive status (know if revoked)
    ///   ✅ Returns metadata (created date, expiry, role)
    /// 
    /// Filtering:
    ///   Currently returns ALL keys (active + revoked).
    ///   Future: Add query param ?active=true to filter.
    /// 
    /// Authorization:
    ///   Returns only keys created by current user.
    ///   Prevents admins from seeing each other's keys.
    ///   Alternative: Return all keys (transparency for team).
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetApiKeys()
    {
        // Get current user ID
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User ID not found in token." });
        }

        // Query keys created by current user
        var keys = await _context.ApiKeys
            .Where(k => k.CreatedByUserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.IsAdmin,
                k.IsActive,
                k.CreatedAt,
                k.ExpiresAt
                // ❌ NOT included: KeyHash (sensitive!)
                // ❌ NOT included: full key (can't recover!)
            })
            .ToListAsync();

        return Ok(keys);
    }

    /// <summary>
    /// Gets a single API key by ID.
    /// </summary>
    /// <param name="id">API key ID.</param>
    /// <returns>
    /// 200 OK with API key details (without plaintext key).
    /// 404 Not Found if key doesn't exist or not owned by current user.
    /// </returns>
    /// <remarks>
    /// This endpoint exists for CreatedAtAction() in GenerateApiKey().
    /// Returns same format as list endpoint (no plaintext key!).
    /// </remarks>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApiKey(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var key = await _context.ApiKeys
            .Where(k => k.Id == id && k.CreatedByUserId == userId)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.IsAdmin,
                k.IsActive,
                k.CreatedAt,
                k.ExpiresAt
            })
            .FirstOrDefaultAsync();

        if (key == null)
        {
            return NotFound(new { error = "API key not found." });
        }

        return Ok(key);
    }

    /// <summary>
    /// Revokes an API key (soft delete).
    /// </summary>
    /// <param name="id">API key ID to revoke.</param>
    /// <returns>
    /// 204 No Content if revocation successful.
    /// 404 Not Found if key doesn't exist.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if not admin.
    /// </returns>
    /// <remarks>
    /// Revocation:
    ///   - Sets IsActive = false (soft delete)
    ///   - Key stops working immediately
    ///   - Audit trail preserved (can see it was revoked)
    ///   - Can be reactivated if needed (future feature)
    /// 
    /// Idempotency:
    ///   Revoking already-revoked key succeeds (204 No Content).
    ///   HTTP DELETE should be idempotent per RFC 7231.
    /// 
    /// Security:
    ///   Immediate effect - revoked keys fail validation right away.
    ///   No grace period (if compromised, must revoke ASAP).
    /// 
    /// Alternative: Hard Delete
    ///   Could DELETE FROM api_keys instead of soft delete.
    ///   Pros: Simpler, truly "gone"
    ///   Cons: Lose audit trail, can't reactivate
    ///   Our choice: Soft delete (better for security/compliance)
    /// </remarks>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeApiKey(string id)
    {
        try
        {
            await _apiKeyService.RevokeKeyAsync(id);
            return NoContent();  // 204 No Content
        }
        catch (InvalidOperationException)
        {
            // Key not found
            return NotFound(new { error = $"API key with ID '{id}' not found." });
        }
    }
}

/// <summary>
/// Request model for generating a new API key.
/// </summary>
public class GenerateApiKeyRequest
{
    /// <summary>
    /// Human-readable name for the API key.
    /// </summary>
    /// <example>Production Deploy Bot</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the key should have Admin role (write access).
    /// </summary>
    /// <example>true</example>
    public bool IsAdmin { get; set; }
}
