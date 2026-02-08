namespace Hamco.Core.Models;

/// <summary>
/// Represents a slogan displayed in the Hamco UI.
/// This is an admin-only entity - no public access to slogans API.
/// </summary>
/// <remarks>
/// Maps to the 'slogans' table in PostgreSQL database.
/// 
/// Design decisions:
/// - Admin-only CRUD operations (no public access)
/// - IsActive flag controls which slogans appear in random rotation
/// - CreatedByUserId tracks which admin created each slogan
/// - Random endpoint exists but is admin-only (UI will fetch server-side)
/// 
/// Usage:
/// - Admins create slogans via API
/// - UI server-side fetches random active slogan
/// - Slogans displayed to all users in UI
/// - Only admins can see/edit/delete via API
/// </remarks>
public class Slogan
{
    /// <summary>
    /// Primary key. Auto-incremented by PostgreSQL.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// The slogan text to display. Required field.
    /// </summary>
    /// <remarks>
    /// Examples:
    ///   "Your AI workspace, everywhere"
    ///   "Code. Deploy. Manage. From anywhere."
    ///   "The developer's command center"
    /// </remarks>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this slogan is active and can appear in random rotation.
    /// </summary>
    /// <remarks>
    /// Only active slogans (IsActive = true) are returned by the random endpoint.
    /// This allows admins to temporarily disable slogans without deleting them.
    /// </remarks>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// UTC timestamp when this slogan was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// ID of the admin user who created this slogan. Nullable for system-created slogans.
    /// </summary>
    /// <remarks>
    /// This field tracks accountability - which admin created each slogan.
    /// Can be null for slogans created via database seeding or migration.
    /// </remarks>
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// UTC timestamp when this slogan was last updated. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
