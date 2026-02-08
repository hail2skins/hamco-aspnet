using Hamco.Data;
using Hamco.Services;
using Hamco.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hamco.Api.Controllers;

/// <summary>
/// MVC controller for rendering note views (HTML pages).
/// Separate from NotesController (API) to maintain clean separation.
/// </summary>
public class NotesViewController : BaseController
{
    private readonly HamcoDbContext _context;
    private readonly IMarkdownService _markdownService;

    public NotesViewController(
        HamcoDbContext context,
        IMarkdownService markdownService,
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer)
        : base(sloganRandomizer, imageRandomizer)
    {
        _context = context;
        _markdownService = markdownService;
    }

    /// <summary>
    /// Display list of all notes.
    /// </summary>
    [HttpGet("/notes")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Title = "Notes";
        ViewBag.Heading = "Technical Notes & Articles";

        var notes = await _context.Notes
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View(notes);
    }

    /// <summary>
    /// Display a single note with rendered markdown.
    /// </summary>
    [HttpGet("/notes/{id}")]
    public async Task<IActionResult> Detail(int id)
    {
        var note = await _context.Notes.FindAsync(id);

        if (note == null)
        {
            return NotFound();
        }

        ViewBag.Title = note.Title;
        ViewBag.Heading = note.Title;
        ViewBag.Author = "Art Mills";
        ViewBag.CreatedAt = note.CreatedAt.ToString("MMMM dd, yyyy");
        ViewBag.ShowNoteMetadata = true;

        // Render markdown to HTML with syntax highlighting
        ViewBag.RenderedContent = _markdownService.RenderToHtmlWithSyntaxHighlight(note.Content);

        return View(note);
    }
}
