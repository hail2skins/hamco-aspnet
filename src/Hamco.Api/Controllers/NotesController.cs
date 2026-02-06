using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hamco.Core.Models;
using Hamco.Core.Utilities;
using Hamco.Data;
using System.Security.Claims;

namespace Hamco.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly HamcoDbContext _context;

    public NotesController(HamcoDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> CreateNote(CreateNoteRequest request)
    {
        // Get user ID from JWT claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Create the note
        var note = new Note
        {
            Title = request.Title,
            Slug = SlugGenerator.GenerateSlug(request.Title),
            Content = request.Content,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        // Return response
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

        return CreatedAtAction(nameof(CreateNote), new { id = note.Id }, response);
    }
}
