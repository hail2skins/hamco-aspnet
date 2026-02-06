using Microsoft.AspNetCore.Mvc;
using Hamco.Core.Models;
using Hamco.Core.Utilities;
using Hamco.Data;

namespace Hamco.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        // Create the note
        var note = new Note
        {
            Title = request.Title,
            Slug = SlugGenerator.GenerateSlug(request.Title),
            Content = request.Content,
            UserId = null, // No auth for now
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
