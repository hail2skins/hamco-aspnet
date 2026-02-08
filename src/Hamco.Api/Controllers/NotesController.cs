using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Core.Utilities;
using Hamco.Data;

namespace Hamco.Api.Controllers;

/// <summary>
/// API controller for managing blog notes (posts).
/// Provides CRUD (Create, Read, Update, Delete) operations with role-based authorization.
/// </summary>
/// <remarks>
/// REST API endpoints:
///   POST   /api/notes      - Create new note (Admin only)
///   GET    /api/notes/{id} - Get single note by ID (Public)
///   GET    /api/notes      - Get all notes (Public)
///   PUT    /api/notes/{id} - Update existing note (Admin only)
///   DELETE /api/notes/{id} - Delete note (Admin only)
/// 
/// Authorization Model - Blog-Style Architecture:
/// 
///   PUBLIC READ (No authentication required):
///     - GET /api/notes/{id} - Anyone can read a specific note
///     - GET /api/notes      - Anyone can list all notes
///     
///     Why public read? Blog content should be accessible to everyone.
///     This enables SEO, social sharing, and open access to information.
/// 
///   ADMIN-ONLY WRITE (Authentication + Admin role required):
///     - POST   /api/notes      - Only admins can create notes
///     - PUT    /api/notes/{id} - Only admins can edit notes
///     - DELETE /api/notes/{id} - Only admins can delete notes
///     
///     Why admin-only write? Content management requires elevated privileges.
///     This prevents spam, vandalism, and unauthorized content changes.
/// 
///   How to become admin:
///     - The FIRST user registered automatically becomes admin (IsAdmin = true)
///     - Subsequent users are regular users (IsAdmin = false)
///     - Additional admins can be promoted via database if needed
/// 
/// Controller Design:
///   - [ApiController]: Enables API-specific behaviors (auto model validation)
///   - [Route]: Defines base URL pattern for all actions
///   - No [Authorize] at class level: GET endpoints are intentionally public
///   - Individual POST/PUT/DELETE methods have [Authorize(Roles = "Admin")]
/// 
/// Real-world analogy:
///   Think of a newspaper website:
///   - Anyone can read articles (public GET)
///   - Only journalists can publish (admin POST)
///   - Only editors can modify articles (admin PUT/DELETE)
/// 
/// Async/await pattern:
///   All methods use 'async Task' for non-blocking I/O operations.
///   Database calls don't block threads while waiting for results.
///   Improves scalability (handle more concurrent requests).
/// </remarks>
[ApiController]  // Enables automatic model validation and API conventions
[Route("api/[controller]")]  // Route pattern: /api/notes ([controller] = Notes)
// NOTE: No [Authorize] at class level - GET endpoints are public (blog is readable by anyone)
// Individual POST/PUT/DELETE methods have [Authorize(Roles = "Admin")]
public class NotesController : ControllerBase
{
    // Private field to store database context
    // 'readonly' means it can only be set in constructor (immutable)
    // Naming convention: _camelCase for private fields
    private readonly HamcoDbContext _context;

    /// <summary>
    /// Initializes a new instance of the NotesController.
    /// </summary>
    /// <param name="context">
    /// Database context injected by ASP.NET Core DI container.
    /// </param>
    /// <remarks>
    /// Dependency Injection (DI) in action:
    ///   1. HamcoDbContext registered in Program.cs (AddDbContext)
    ///   2. Controller declares dependency in constructor parameter
    ///   3. DI container creates HamcoDbContext instance
    ///   4. DI container creates NotesController with context
    ///   5. Controller uses context for database operations
    /// 
    /// Benefits of DI:
    ///   ✅ Testability (can inject mock context for tests)
    ///   ✅ Loose coupling (controller doesn't create context)
    ///   ✅ Lifecycle management (DI handles creation/disposal)
    ///   ✅ Configuration centralized (Program.cs)
    /// 
    /// Constructor injection is the most common DI pattern in ASP.NET Core.
    /// </remarks>
    public NotesController(HamcoDbContext context)
    {
        // Store injected context in private field
        // '_context' will be used in all action methods
        _context = context;
    }

    /// <summary>
    /// Creates a new blog note. Requires admin authentication.
    /// </summary>
    /// <param name="request">
    /// Note creation data (title and content).
    /// Validated automatically by ASP.NET Core model binding.
    /// </param>
    /// <returns>
    /// 201 Created with note data if successful.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if authenticated but not admin.
    /// 400 Bad Request if validation fails.
    /// </returns>
    /// <remarks>
    /// HTTP Method: POST /api/notes
    /// 
    /// ⚠️ AUTHORIZATION REQUIRED: Admin role only
    /// 
    /// To call this endpoint:
    /// 1. Register or login as the first user (becomes admin automatically)
    /// 2. Include JWT token in Authorization header:
    ///    Authorization: Bearer {your_jwt_token}
    /// 3. Token must belong to a user with IsAdmin = true
    /// 
    /// Request body (JSON):
    /// {
    ///   "title": "My First Post",
    ///   "content": "This is the content..."
    /// }
    /// 
    /// Response (201 Created):
    /// {
    ///   "id": 1,
    ///   "title": "My First Post",
    ///   "slug": "my-first-post",
    ///   "content": "This is the content...",
    ///   "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    ///   "createdAt": "2026-02-06T14:30:00Z",
    ///   "updatedAt": "2026-02-06T14:30:00Z"
    /// }
    /// 
    /// [HttpPost] attribute:
    ///   - Maps this method to HTTP POST requests
    ///   - Combined with route: POST /api/notes
    /// 
    /// [Authorize(Roles = "Admin")] attribute:
    ///   - Requires valid JWT token (authentication)
    ///   - Requires "Admin" role in token claims (authorization)
    ///   - Returns 401 if no token or invalid token
    ///   - Returns 403 if token valid but user not admin
    /// 
    /// How user ID is determined:
    ///   - Extracted from JWT token claims (ClaimTypes.NameIdentifier)
    ///   - NOT provided in request body (prevent spoofing)
    ///   - Ensures note is linked to authenticated user
    /// 
    /// async Task<ActionResult<NoteResponse>> explained:
    ///   - 'async': Method can use 'await' for async operations
    ///   - 'Task': Represents asynchronous operation
    ///   - 'ActionResult': Base type for all action results (OK, NotFound, etc.)
    ///   - 'ActionResult<NoteResponse>': Can return NoteResponse or any ActionResult
    /// 
    /// Model binding:
    ///   ASP.NET Core automatically deserializes JSON body to CreateNoteRequest.
    ///   Validation attributes ([Required], etc.) are checked automatically.
    ///   If validation fails, returns 400 Bad Request (no need to check manually).
    /// 
    /// CreatedAtAction() explained:
    ///   Returns 201 Created status code (standard for resource creation).
    ///   Includes Location header: /api/notes/1 (URL of created resource).
    ///   Follows REST conventions (POST should return created resource).
    /// 
    /// Security considerations:
    ///   ✅ User ID from JWT token (can't forge ownership)
    ///   ✅ Admin-only access (prevents spam/unauthorized content)
    ///   ✅ Input validation (prevents XSS, injection attacks)
    ///   ⚠️ No content sanitization (should validate HTML/markdown)
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin")]  // Only administrators can create notes
    public async Task<ActionResult<NoteResponse>> CreateNote(CreateNoteRequest request)
    {
        // Step 1: Detect authentication method and extract user ID
        var authMethod = User.FindFirst("auth_method")?.Value;
        string? userId = null;
        
        if (authMethod == "api_key")
        {
            // API key authentication - UserId will be null
            // API keys don't have a corresponding user in the Users table
            userId = null;
        }
        else
        {
            // JWT authentication - extract user ID from token
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                // Should never happen if [Authorize] is working correctly
                // But defensive programming: verify we have a user ID
                return Unauthorized(new { message = "User ID not found in token" });
            }
        }
        
        // Step 2: Create Note entity from request DTO
        // Object initializer syntax: new Type { Property = value, ... }
        // More readable than: var note = new Note(); note.Title = ...; note.Slug = ...;
        var note = new Note
        {
            Title = request.Title,  // From user input
            Slug = SlugGenerator.GenerateSlug(request.Title),  // Auto-generated
            Content = request.Content,  // From user input
            UserId = userId,  // From authenticated user's JWT token, or null for API keys
            CreatedAt = DateTime.UtcNow,  // Current UTC time
            UpdatedAt = DateTime.UtcNow   // Same as CreatedAt initially
        };
        // Note: Id is not set (database will auto-generate)

        // Step 3: Add note to EF Core change tracker
        // _context.Notes.Add() tells EF "I want to insert this"
        // Doesn't execute SQL yet (just tracks the intent)
        // EF will generate: INSERT INTO notes (...) VALUES (...)
        _context.Notes.Add(note);
        
        // Step 4: Save changes to database
        // 'await' keyword pauses execution until database operation completes
        // While waiting, thread is freed to handle other requests (non-blocking)
        // SaveChangesAsync() executes all pending operations in a transaction:
        //   1. Generate SQL: INSERT INTO notes (title, slug, ...) VALUES (?, ?, ...)
        //   2. Execute SQL in database
        //   3. Retrieve generated ID (auto-increment)
        //   4. Update note.Id with generated value
        // If exception occurs, transaction rolls back (no partial saves)
        await _context.SaveChangesAsync();
        
        // After SaveChangesAsync(), note.Id is populated by database!

        // Step 5: Map Note entity to NoteResponse DTO
        // Why create response object?
        //   - Response might differ from entity (hide fields, add computed fields)
        //   - Consistent API contract (separate from database model)
        //   - Could include additional data (links, metadata, etc.)
        var response = new NoteResponse
        {
            Id = note.Id,            // Now populated by database
            Title = note.Title,
            Slug = note.Slug,
            Content = note.Content,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
        // Note: We don't expose DeletedAt in response (always null for new notes)

        // Step 6: Return 201 Created response
        // CreatedAtAction() parameters:
        //   1. nameof(CreateNote): Action name for Location header URL
        //   2. new { id = note.Id }: Route values for URL (api/notes/123)
        //   3. response: Response body (serialized to JSON)
        // 
        // HTTP response:
        //   Status: 201 Created
        //   Location: /api/notes/1
        //   Body: { "id": 1, "title": "...", ... }
        // 
        // nameof() explained:
        //   Gets method name as string ("CreateNote")
        //   Refactoring-safe (if method renamed, nameof updates automatically)
        //   Alternative: Magic string "CreateNote" (bad, breaks on rename)
        return CreatedAtAction(nameof(CreateNote), new { id = note.Id }, response);
    }

    /// <summary>
    /// Retrieves a single note by ID. Public access - no authentication required.
    /// </summary>
    /// <param name="id">The unique identifier of the note to retrieve.</param>
    /// <returns>
    /// 200 OK with note data if found and not deleted.
    /// 404 Not Found if note doesn't exist or was deleted.
    /// </returns>
    /// <remarks>
    /// HTTP Method: GET /api/notes/{id}
    /// 
    /// ✅ PUBLIC ENDPOINT - No authentication required!
    /// 
    /// This endpoint is intentionally public to allow:
    ///   - Anyone to read blog posts (SEO-friendly)
    ///   - Social sharing without login barriers
    ///   - Open access to published content
    /// 
    /// Example: GET /api/notes/1
    /// 
    /// Response (200 OK):
    /// {
    ///   "id": 1,
    ///   "title": "My Blog Post",
    ///   "slug": "my-blog-post",
    ///   "content": "Content here...",
    ///   "userId": "a1b2c3d4-...",
    ///   "createdAt": "2026-02-06T14:30:00Z",
    ///   "updatedAt": "2026-02-06T14:30:00Z"
    /// }
    /// 
    /// Response (404 Not Found):
    ///   Returned if note doesn't exist
    ///   Also returned if note was deleted (soft delete, when implemented)
    /// 
    /// Route parameter:
    ///   {id} in [HttpGet("{id}")] matches 'int id' parameter
    ///   ASP.NET Core automatically converts URL string to int
    ///   If conversion fails (e.g., GET /api/notes/abc), returns 400 Bad Request
    /// 
    /// Why no [Authorize] attribute?
    ///   - Blogs are typically readable by everyone
    ///   - No sensitive information in published posts
    ///   - Improves accessibility and SEO
    /// 
    /// FindAsync() vs FirstOrDefaultAsync():
    ///   FindAsync: Fast lookup by primary key (uses EF Core cache)
    ///   FirstOrDefaultAsync: Query with conditions (slower, always hits DB)
    ///   We use FindAsync because we're looking up by ID (primary key).
    /// </remarks>
    [HttpGet("{id}")]  // Route: GET /api/notes/123
    public async Task<ActionResult<NoteResponse>> GetNote(int id)
    {
        // FindAsync() queries database for note with matching Id
        // Returns Note object if found, null if not found
        // 'await' waits for database query to complete
        var note = await _context.Notes.FindAsync(id);

        // Check if note exists and is not soft-deleted
        // '||' is logical OR (either condition triggers)
        // 'note == null': Note doesn't exist in database
        // 'note.DeletedAt != null': Note exists but is marked deleted
        if (note == null || note.DeletedAt != null)
        {
            // NotFound() returns 404 Not Found status code
            // No response body (standard for 404)
            // HTTP response: 404 Not Found
            return NotFound();
        }

        // Map entity to response DTO (same pattern as CreateNote)
        var response = new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            Slug = note.Slug,
            Content = note.Content,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };

        // Ok() returns 200 OK status code with response body
        // HTTP response: 200 OK
        // Body: { "id": 1, "title": "...", ... }
        return Ok(response);
    }

    /// <summary>
    /// Retrieves all notes (excludes soft-deleted notes). Public access - no authentication required.
    /// </summary>
    /// <returns>
    /// 200 OK with array of note data.
    /// Returns empty array if no notes exist.
    /// </returns>
    /// <remarks>
    /// HTTP Method: GET /api/notes
    /// 
    /// ✅ PUBLIC ENDPOINT - No authentication required!
    /// 
    /// This endpoint is intentionally public to allow:
    ///   - Anyone to browse all blog posts
    ///   - RSS feed readers to access content
    ///   - Search engines to index posts (SEO)
    ///   - Frontend SPAs to display posts without login
    /// 
    /// Response (200 OK):
    /// [
    ///   { 
    ///     "id": 1, 
    ///     "title": "First Post", 
    ///     "slug": "first-post",
    ///     "content": "Content...",
    ///     "userId": "a1b2c3d4-...",
    ///     "createdAt": "2026-02-06T14:30:00Z",
    ///     "updatedAt": "2026-02-06T14:30:00Z"
    ///   },
    ///   { 
    ///     "id": 2, 
    ///     "title": "Second Post",
    ///     "slug": "second-post", 
    ///     ...
    ///   }
    /// ]
    /// 
    /// Returns empty array [] if no notes exist (not an error).
    /// 
    /// Why public access for listing?
    ///   - Blog homepages typically show all posts
    ///   - No sensitive data in published posts
    ///   - Consistent with public GET /api/notes/{id}
    /// 
    /// LINQ query:
    ///   _context.Notes.Where(...).ToListAsync()
    ///   
    /// LINQ (Language Integrated Query):
    ///   C# syntax for querying collections (lists, arrays, databases)
    ///   Looks like SQL but integrated into C#
    ///   EF Core translates LINQ to SQL
    /// 
    /// Query translation:
    ///   C# LINQ: Where(n => n.DeletedAt == null)
    ///   SQL: WHERE deleted_at IS NULL
    ///   
    /// Performance consideration:
    ///   This loads ALL non-deleted notes into memory.
    ///   For large datasets, add pagination:
    ///   - Skip/Take for offset-based pagination
    ///   - Cursor-based pagination for better performance
    ///   
    /// Future improvement: GET /api/notes?page=1&amp;pageSize=20
    /// </remarks>
    [HttpGet]  // Route: GET /api/notes (no parameters)
    public async Task<ActionResult<List<NoteResponse>>> GetNotes()
    {
        // Query database for all non-deleted notes
        // _context.Notes: DbSet (represents notes table)
        // .Where(...): Filter rows (LINQ query method)
        // Lambda: n => n.DeletedAt == null
        //   - 'n': Parameter (represents each note)
        //   - '=>': Lambda arrow (separates parameter from expression)
        //   - 'n.DeletedAt == null': Condition (only include non-deleted)
        // .ToListAsync(): Execute query and return results as List
        //   - Async version of ToList()
        //   - Actually sends SQL to database
        //   - Returns List<Note>
        var notes = await _context.Notes
            .Where(n => n.DeletedAt == null)
            .ToListAsync();
        
        // SQL generated:
        // SELECT id, title, slug, content, user_id, created_at, updated_at, deleted_at
        // FROM notes
        // WHERE deleted_at IS NULL;

        // Map each Note entity to NoteResponse DTO
        // .Select(...): LINQ projection (transforms each item)
        // Lambda: note => new NoteResponse { ... }
        //   Takes each note, creates new NoteResponse
        // .ToList(): Convert IEnumerable to List
        var response = notes.Select(note => new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            Slug = note.Slug,
            Content = note.Content,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        }).ToList();
        
        // Alternative syntax (method chain):
        // var response = notes
        //     .Select(note => new NoteResponse { ... })
        //     .ToList();
        
        // Note: This mapping happens in-memory (after database query)
        // For better performance with large datasets, use projection in query:
        // var response = await _context.Notes
        //     .Where(n => n.DeletedAt == null)
        //     .Select(n => new NoteResponse { ... })
        //     .ToListAsync();
        // This would only select needed columns in SQL

        // Ok() returns 200 OK with list (even if empty)
        // HTTP response: 200 OK
        // Body: [...] (array of notes, or empty [])
        return Ok(response);
    }

    /// <summary>
    /// Updates an existing note's title and content. Requires admin authentication.
    /// </summary>
    /// <param name="id">The unique identifier of the note to update.</param>
    /// <param name="request">
    /// Updated note data (title and content).
    /// Validated automatically by model binding.
    /// </param>
    /// <returns>
    /// 200 OK with updated note data if successful.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if authenticated but not admin.
    /// 404 Not Found if note doesn't exist or was deleted.
    /// 400 Bad Request if validation fails.
    /// </returns>
    /// <remarks>
    /// HTTP Method: PUT /api/notes/{id}
    /// 
    /// ⚠️ AUTHORIZATION REQUIRED: Admin role only
    /// 
    /// To call this endpoint:
    /// 1. Must be authenticated with valid JWT token
    /// 2. User must have admin role (IsAdmin = true)
    /// 3. Include token in Authorization header
    /// 
    /// Request: PUT /api/notes/1
    /// Authorization: Bearer {admin_token}
    /// Content-Type: application/json
    /// 
    /// Body:
    /// {
    ///   "title": "Updated Title",
    ///   "content": "Updated content..."
    /// }
    /// 
    /// Response (200 OK):
    /// {
    ///   "id": 1,
    ///   "title": "Updated Title",
    ///   "slug": "updated-title",  ← Regenerated from new title!
    ///   "content": "Updated content...",
    ///   "userId": "a1b2c3d4-...",
    ///   "createdAt": "2026-02-06T14:30:00Z",
    ///   "updatedAt": "2026-02-06T15:45:00Z"  ← Updated timestamp
    /// }
    /// 
    /// PUT vs PATCH:
    ///   PUT: Replace entire resource (all fields required)
    ///   PATCH: Partial update (only changed fields)
    ///   We use PUT but only update title/content (hybrid approach)
    /// 
    /// EF Core change tracking:
    ///   1. FindAsync() loads note, EF tracks it
    ///   2. Modify properties (note.Title = ...)
    ///   3. SaveChangesAsync() detects changes automatically
    ///   4. EF generates UPDATE SQL for changed fields only
    ///   5. No need to manually mark as modified!
    /// 
    /// Slug regeneration:
    ///   When title changes, we regenerate slug to keep them in sync.
    ///   Trade-off: Breaks existing URLs if title changes!
    ///   Alternative: Allow manual slug editing, separate from title.
    /// 
    /// Why admin-only?
    ///   - Prevents unauthorized edits to published content
    ///   - Ensures content quality control
    ///   - Creates clear audit trail (who modified what)
    /// </remarks>
    [HttpPut("{id}")]  // Route: PUT /api/notes/123
    [Authorize(Roles = "Admin")]  // Only administrators can update notes
    public async Task<ActionResult<NoteResponse>> UpdateNote(int id, UpdateNoteRequest request)
    {
        // Step 1: Find existing note
        var note = await _context.Notes.FindAsync(id);

        // Step 2: Check if note exists and not deleted
        if (note == null || note.DeletedAt != null)
        {
            return NotFound();
        }

        // Step 3: Update note properties
        // EF Core tracks these changes automatically!
        note.Title = request.Title;
        note.Slug = SlugGenerator.GenerateSlug(request.Title);  // Regenerate slug from new title
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;  // Update timestamp to current time
        
        // Note: We don't update CreatedAt (preserve original creation time)
        // Note: We don't update Id (primary key should never change)
        // Note: We don't update UserId (would require authorization check)

        // Step 4: Save changes to database
        // EF Core knows note was loaded and modified
        // Generates: UPDATE notes SET title=?, slug=?, content=?, updated_at=? WHERE id=?
        // Only includes changed columns (efficient!)
        await _context.SaveChangesAsync();

        // Step 5: Return updated note
        var response = new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            Slug = note.Slug,
            Content = note.Content,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };

        // Ok() returns 200 OK (standard for successful update)
        // Alternative: 204 No Content (no response body)
        // We return updated note to confirm changes
        return Ok(response);
    }

    /// <summary>
    /// Deletes a note from the database (hard delete). Requires admin authentication.
    /// </summary>
    /// <param name="id">The unique identifier of the note to delete.</param>
    /// <returns>
    /// 204 No Content if successful.
    /// 401 Unauthorized if not authenticated.
    /// 403 Forbidden if authenticated but not admin.
    /// 404 Not Found if note doesn't exist or already deleted.
    /// </returns>
    /// <remarks>
    /// HTTP Method: DELETE /api/notes/{id}
    /// 
    /// ⚠️ AUTHORIZATION REQUIRED: Admin role only
    /// 
    /// To call this endpoint:
    /// 1. Must be authenticated with valid JWT token
    /// 2. User must have admin role (IsAdmin = true)
    /// 3. Include token in Authorization header
    /// 
    /// Example: DELETE /api/notes/1
    /// Authorization: Bearer {admin_token}
    /// 
    /// Response: 204 No Content (no response body)
    /// 
    /// Hard delete vs Soft delete:
    ///   Hard delete (current): Removes row from database (permanent)
    ///   Soft delete (better): Sets DeletedAt timestamp (recoverable)
    /// 
    /// Current implementation: HARD DELETE
    ///   _context.Notes.Remove(note) physically deletes the row.
    ///   SQL: DELETE FROM notes WHERE id = ?
    /// 
    /// To implement soft delete (recommended):
    ///   Instead of: _context.Notes.Remove(note);
    ///   Use:        note.DeletedAt = DateTime.UtcNow;
    ///   SQL:        UPDATE notes SET deleted_at = ? WHERE id = ?
    /// 
    /// Benefits of soft delete:
    ///   ✅ Can restore deleted items
    ///   ✅ Preserves audit trail
    ///   ✅ Maintains referential integrity
    ///   ✅ Allows "Recycle Bin" feature
    /// 
    /// Drawbacks of soft delete:
    ///   ⚠️ Database grows (never truly deletes)
    ///   ⚠️ Must filter deleted_at in all queries
    ///   ⚠️ Unique constraints complicated (allow duplicates if deleted)
    /// 
    /// 204 No Content explained:
    ///   Standard HTTP status for successful DELETE with no response body.
    ///   Indicates: "I did what you asked, nothing to return."
    ///   Alternative: 200 OK with success message (less RESTful).
    /// 
    /// Why admin-only deletion?
    ///   - Prevents accidental or malicious content removal
    ///   - Maintains content integrity
    ///   - Creates clear audit trail
    ///   - Aligns with blog-style architecture (controlled publishing)
    /// </remarks>
    [HttpDelete("{id}")]  // Route: DELETE /api/notes/123
    [Authorize(Roles = "Admin")]  // Only administrators can delete notes
    public async Task<IActionResult> DeleteNote(int id)
    {
        // Step 1: Find note to delete
        var note = await _context.Notes.FindAsync(id);

        // Step 2: Check if note exists and not already deleted
        if (note == null || note.DeletedAt != null)
        {
            return NotFound();
        }

        // Step 3: Remove note from database (HARD DELETE)
        // _context.Notes.Remove() marks note for deletion
        // EF will generate: DELETE FROM notes WHERE id = ?
        _context.Notes.Remove(note);
        
        // Alternative for SOFT DELETE (better approach):
        // note.DeletedAt = DateTime.UtcNow;
        // This would UPDATE instead of DELETE
        
        // Step 4: Save changes (execute DELETE)
        await _context.SaveChangesAsync();

        // Step 5: Return 204 No Content
        // NoContent() returns 204 status code with no body
        // IActionResult return type (not ActionResult<T>) because no body
        // HTTP response: 204 No Content
        return NoContent();
    }
}
