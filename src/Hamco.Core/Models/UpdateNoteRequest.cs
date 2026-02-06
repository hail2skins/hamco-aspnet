using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

/// <summary>
/// Data Transfer Object (DTO) for updating an existing note via PUT /api/notes/{id}.
/// Contains only fields that can be modified by users.
/// </summary>
/// <remarks>
/// Why is this identical to CreateNoteRequest?
/// 
/// Good question! They're the same NOW, but keeping them separate allows:
///   - Future differences (e.g., optional fields on update)
///   - Clear API intent (create vs update are different operations)
///   - Independent validation rules
///   - Versioning flexibility
/// 
/// Example future differences:
///   - Update might make Title optional (keep existing if not provided)
///   - Update might add "tags" or "published" fields
///   - Create might require different validation
/// 
/// This is the Single Responsibility Principle:
///   - CreateNoteRequest: Validates data for creating notes
///   - UpdateNoteRequest: Validates data for updating notes
/// 
/// Even if they're identical now, separation is good design!
/// </remarks>
public class UpdateNoteRequest
{
    /// <summary>
    /// The updated title. Required, 1-255 characters.
    /// </summary>
    /// <remarks>
    /// When a note is updated, the slug is regenerated from the new title.
    /// See NotesController.UpdateNote() for implementation.
    /// 
    /// Example:
    ///   Original title: "My First Post" (slug: "my-first-post")
    ///   Updated title: "My Awesome Post" (slug: "my-awesome-post")
    /// 
    /// This ensures slugs always match titles.
    /// 
    /// Trade-off: Changing slug breaks existing URLs!
    /// Future improvement: Make slug editable separately.
    /// </remarks>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters")]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// The updated content. Required, no length limit.
    /// </summary>
    /// <remarks>
    /// UpdatedAt timestamp is automatically set to DateTime.UtcNow
    /// when this request is processed in the controller.
    /// 
    /// Users don't provide UpdatedAt - it's managed by the server.
    /// This is why we use DTOs instead of accepting Note entity directly!
    /// </remarks>
    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
}
