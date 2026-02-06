using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    [HttpGet("{id}")]
    public async Task<ActionResult<NoteResponse>> GetNote(int id)
    {
        var note = await _context.Notes.FindAsync(id);

        if (note == null || note.DeletedAt != null)
        {
            return NotFound();
        }

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

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<List<NoteResponse>>> GetNotes()
    {
        var notes = await _context.Notes
            .Where(n => n.DeletedAt == null)
            .ToListAsync();

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

        return Ok(response);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<NoteResponse>> UpdateNote(int id, UpdateNoteRequest request)
    {
        var note = await _context.Notes.FindAsync(id);

        if (note == null || note.DeletedAt != null)
        {
            return NotFound();
        }

        // Update fields
        note.Title = request.Title;
        note.Slug = SlugGenerator.GenerateSlug(request.Title); // Regenerate slug
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

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

        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNote(int id)
    {
        var note = await _context.Notes.FindAsync(id);

        if (note == null || note.DeletedAt != null)
        {
            return NotFound();
        }

        // Hard delete (remove from database)
        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
