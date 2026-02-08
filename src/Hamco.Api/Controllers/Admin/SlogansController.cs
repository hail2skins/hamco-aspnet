using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Data;

namespace Hamco.Api.Controllers.Admin;

/// <summary>
/// API controller for managing slogans (admin-only operations).
/// Handles CRUD operations for slogans displayed in the Hamco UI.
/// </summary>
/// <remarks>
/// REST API endpoints:
///   GET    /api/slogans         - List all slogans (admin only)
///   POST   /api/slogans         - Create new slogan (admin only)
///   PUT    /api/slogans/{id}    - Update slogan (admin only)
///   DELETE /api/slogans/{id}    - Delete slogan (admin only)
/// 
/// Authorization:
///   ALL endpoints require [Authorize(Roles = "Admin")]!
///   NO public access to slogans API - this is purely for admin CRUD.
///   
///   Why admin-only?
///     - Slogans are part of the brand/UI experience
///     - Only trusted admins should control messaging
///     - Prevents vandalism or inappropriate content
///   
///   How do public users see slogans?
///     - Server-side rendering fetches random slogan from database directly
///     - No public API endpoint - slogans embedded in page HTML
///     - This API is ONLY for admins to manage the slogan list
/// 
/// Security Considerations:
///   🔒 Admin-only operations (no regular users or anonymous access)
///   🔒 CreatedByUserId tracks which admin created each slogan
///   🔒 IsActive flag allows disabling without deleting
///   🔒 Hard delete (not soft delete) - slogans can be permanently removed
/// 
/// Example Usage:
///   # List all slogans
///   curl http://localhost:5250/api/slogans \
///     -H "Authorization: Bearer {admin_jwt}"
///   
///   # Create slogan
///   curl -X POST http://localhost:5250/api/slogans \
///     -H "Authorization: Bearer {admin_jwt}" \
///     -H "Content-Type: application/json" \
///     -d '{"text":"Your AI workspace, everywhere"}'
///   
///   # Update slogan
///   curl -X PUT http://localhost:5250/api/slogans/1 \
///     -H "X-API-Key: hamco_sk_..." \
///     -H "Content-Type: application/json" \
///     -d '{"text":"Updated slogan","isActive":false}'
///   
///   # Delete slogan
///   curl -X DELETE http://localhost:5250/api/slogans/1 \
///     -H "Authorization: Bearer {admin_jwt}"
/// </remarks>
[ApiController]
[Route("api/slogans")]
[Authorize(Roles = "Admin")]  // 🔒 Admin-only controller!
public class SlogansController : ControllerBase
{
    private readonly HamcoDbContext _context;

    /// <summary>
    /// Initializes a new instance of the SlogansController.
    /// </summary>
    /// <param name="context">Database context for querying slogans.</param>
    public SlogansController(HamcoDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/slogans - List all slogans (admin only)
    /// </summary>
    /// <remarks>
    /// Returns all slogans (both active and inactive).
    /// Only accessible to admins.
    /// 
    /// Response format:
    /// [
    ///   {
    ///     "id": 1,
    ///     "text": "Your AI workspace, everywhere",
    ///     "isActive": true,
    ///     "createdAt": "2026-02-08T19:00:00Z",
    ///     "createdByUserId": "guid...",
    ///     "updatedAt": null
    ///   }
    /// ]
    /// </remarks>
    /// <returns>List of all slogans.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<Slogan>>> GetSlogans()
    {
        var slogans = await _context.Slogans
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(slogans);
    }

    /// <summary>
    /// POST /api/slogans - Create new slogan (admin only)
    /// </summary>
    /// <remarks>
    /// Creates a new slogan. Tracks which admin created it via CreatedByUserId.
    /// 
    /// Request body:
    /// {
    ///   "text": "Your AI workspace, everywhere",
    ///   "isActive": true  // optional, defaults to true
    /// }
    /// 
    /// Response (201 Created):
    /// {
    ///   "id": 1,
    ///   "text": "Your AI workspace, everywhere",
    ///   "isActive": true,
    ///   "createdAt": "2026-02-08T19:00:00Z",
    ///   "createdByUserId": "guid...",
    ///   "updatedAt": null
    /// }
    /// </remarks>
    /// <param name="request">Slogan creation request.</param>
    /// <returns>Created slogan with 201 status.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Slogan>> CreateSlogan([FromBody] CreateSloganRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "Slogan text is required" });
        }

        // Get the current admin user ID from JWT claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var slogan = new Slogan
        {
            Text = request.Text.Trim(),
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.Slogans.Add(slogan);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSlogans), new { id = slogan.Id }, slogan);
    }

    /// <summary>
    /// PUT /api/slogans/{id} - Update slogan (admin only)
    /// </summary>
    /// <remarks>
    /// Updates an existing slogan. Sets UpdatedAt timestamp.
    /// 
    /// Request body:
    /// {
    ///   "text": "Updated slogan text",
    ///   "isActive": false
    /// }
    /// 
    /// Both fields are optional - only provided fields are updated.
    /// </remarks>
    /// <param name="id">Slogan ID to update.</param>
    /// <param name="request">Update request with optional text and isActive.</param>
    /// <returns>Updated slogan with 200 status, or 404 if not found.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Slogan>> UpdateSlogan(int id, [FromBody] UpdateSloganRequest request)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound(new { message = "Slogan not found" });
        }

        // Update only provided fields
        if (request.Text != null)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { message = "Slogan text cannot be empty" });
            }
            slogan.Text = request.Text.Trim();
        }

        if (request.IsActive.HasValue)
        {
            slogan.IsActive = request.IsActive.Value;
        }

        slogan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(slogan);
    }

    /// <summary>
    /// DELETE /api/slogans/{id} - Delete slogan (admin only)
    /// </summary>
    /// <remarks>
    /// Permanently deletes a slogan (hard delete, not soft delete).
    /// 
    /// Why hard delete?
    ///   - Slogans are simple text, no complex relationships
    ///   - Easy to recreate if needed
    ///   - No audit trail requirement for slogans
    ///   - Keeps database clean
    /// </remarks>
    /// <param name="id">Slogan ID to delete.</param>
    /// <returns>204 No Content on success, 404 if not found.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteSlogan(int id)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound(new { message = "Slogan not found" });
        }

        _context.Slogans.Remove(slogan);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

/// <summary>
/// Request model for creating a new slogan.
/// </summary>
public class CreateSloganRequest
{
    /// <summary>
    /// The slogan text to display. Required.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the slogan is active. Defaults to true if not provided.
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request model for updating an existing slogan.
/// Both fields are optional - only provided fields are updated.
/// </summary>
public class UpdateSloganRequest
{
    /// <summary>
    /// Updated slogan text. Optional.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Updated active status. Optional.
    /// </summary>
    public bool? IsActive { get; set; }
}
