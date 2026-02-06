using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for creating a new note via POST /api/notes.
/// Validates user input before creating a Note entity.
/// </summary>
/// <remarks>
/// Why use a separate Request class instead of using Note directly?
/// 
/// 1. **Security:** Prevents users from setting Id, CreatedAt, etc.
/// 2. **Validation:** Enforces required fields and constraints
/// 3. **API Design:** Only accepts fields users should provide
/// 4. **Flexibility:** Request model can differ from database model
/// 
/// DTO Pattern (Data Transfer Object):
///   - Request DTOs: Client → Server (what users send)
///   - Response DTOs: Server → Client (what users receive)
///   - Entity Models: Server → Database (what we store)
/// 
/// This separation is a best practice in API design!
/// </remarks>
public class CreateNoteRequest
{
    /// <summary>
    /// The title of the note. Required, 1-255 characters.
    /// </summary>
    /// <remarks>
    /// Data Annotations in C# (attributes for validation):
    /// 
    /// [Required]: Field must have a value (not null, not empty string)
    ///   - Checked automatically by ASP.NET Core model binding
    ///   - If validation fails, returns 400 Bad Request
    /// 
    /// [StringLength]: Enforces min/max length
    ///   - MinimumLength = 1: Prevents empty strings
    ///   - MaximumLength = 255: Matches database column size
    /// 
    /// ErrorMessage: Custom message returned when validation fails
    /// 
    /// These attributes are processed by ASP.NET Core's Model Validation:
    ///   - Runs before controller action executes
    ///   - If invalid, automatically returns 400 with error details
    ///   - No need to manually check in controller!
    /// 
    /// Example error response:
    /// {
    ///   "title": ["Title must be between 1 and 255 characters"]
    /// }
    /// </remarks>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters")]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The content of the note. Required, no length limit.
    /// </summary>
    /// <remarks>
    /// [Required] ensures content is not null or empty.
    /// 
    /// No [StringLength] attribute here because:
    ///   - Content can be long (stored as TEXT in PostgreSQL)
    ///   - TEXT type in PostgreSQL: up to 1GB of text
    ///   - C# string max length: ~2GB (practical limit)
    /// 
    /// Future improvements:
    ///   - Add [MinLength(1)] to prevent empty content
    ///   - Add markdown validation
    ///   - Add profanity filter
    /// 
    /// Note: Default value 'string.Empty' is required even with [Required]
    /// to satisfy C# nullable reference type warnings (C# 8+).
    /// </remarks>
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
}
