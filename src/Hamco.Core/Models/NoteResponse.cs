namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for returning note data to API clients.
/// Controls what fields are exposed in API responses.
/// </summary>
/// <remarks>
/// Why use a Response DTO instead of returning Note entity directly?
/// 
/// 1. **Security:** Hide sensitive fields (e.g., DeletedAt, internal state)
/// 2. **Stability:** Change database schema without breaking API
/// 3. **Performance:** Include only needed fields (lighter payloads)
/// 4. **Flexibility:** Flatten complex relationships, add computed fields
/// 
/// Example: If Note entity had a "DraftContent" field for unpublished edits,
/// we wouldn't include it in NoteResponse (internal use only).
/// 
/// Request/Response pattern:
///   CreateNoteRequest → [Controller] → Note (Entity) → [Database]
///   [Database] → Note (Entity) → [Controller] → NoteResponse
/// 
/// The controller is the translator between API contracts and database models!
/// </remarks>
public class NoteResponse
{
    /// <summary>
    /// Unique identifier for this note.
    /// </summary>
    /// <remarks>
    /// Exposing the database ID in API responses is debated:
    /// 
    /// Pros:
    ///   - Simple and efficient for lookups (GET /api/notes/123)
    ///   - Standard practice in REST APIs
    ///   - Easy to use in client applications
    /// 
    /// Cons:
    ///   - Reveals database implementation details
    ///   - Allows enumeration (users can guess IDs)
    ///   - Hard to change later if needed
    /// 
    /// Alternative: Use GUIDs or UUIDs for public-facing IDs
    /// 
    /// For this project, int IDs are fine (learning/demo purposes).
    /// </remarks>
    public int Id { get; set; }
    
    /// <summary>
    /// The note's title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// URL-friendly slug generated from the title.
    /// </summary>
    /// <remarks>
    /// Clients can use this for SEO-friendly URLs:
    ///   - Bad: /posts/123
    ///   - Good: /posts/my-awesome-blog-post
    ///   - Better: /posts/123/my-awesome-blog-post (ID for lookup, slug for SEO)
    /// 
    /// Slug is read-only from API perspective (auto-generated server-side).
    /// </remarks>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// The full content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the user who created this note. Null for anonymous notes.
    /// </summary>
    /// <remarks>
    /// Currently always null (authentication not enforced).
    /// 
    /// When authentication is enabled, this will contain the user ID
    /// extracted from the JWT token.
    /// 
    /// Security consideration: Exposing UserId allows tracking who wrote what.
    /// In a multi-user system, consider privacy implications!
    /// </remarks>
    public string? UserId { get; set; }
    
    /// <summary>
    /// UTC timestamp when this note was created.
    /// </summary>
    /// <remarks>
    /// Returned in ISO 8601 format: "2026-02-06T13:30:00Z"
    /// 
    /// The 'Z' suffix means UTC (Zulu time zone).
    /// 
    /// JavaScript clients can parse this directly:
    ///   const date = new Date("2026-02-06T13:30:00Z");
    ///   console.log(date.toLocaleString()); // Converts to user's local time
    /// 
    /// C# DateTime serialization to JSON is automatic via System.Text.Json.
    /// </remarks>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// UTC timestamp when this note was last updated.
    /// </summary>
    /// <remarks>
    /// Will be equal to CreatedAt if note has never been updated.
    /// 
    /// Clients can use this to show "Last edited" timestamps.
    /// 
    /// Note: We don't expose DeletedAt in the response because:
    ///   - Deleted notes shouldn't be returned at all (filtered in queries)
    ///   - If a note is soft-deleted, GET returns 404 (not the note with DeletedAt)
    /// </remarks>
    public DateTime UpdatedAt { get; set; }
}
