using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Core.Utilities;
using Hamco.Data;
using Hamco.Services;

namespace Hamco.Api.Controllers.Admin;

/// <summary>
/// MVC controller for admin notes management (CRUD pages).
/// Provides browser-based interface for admins to manage blog notes.
/// </summary>
/// <remarks>
/// Routes:
///   GET  /admin/notes          - List all notes
///   GET  /admin/notes/create   - Show create form
///   POST /admin/notes/create   - Handle create submission
///   GET  /admin/notes/edit/{id}   - Show edit form
///   POST /admin/notes/edit/{id}   - Handle edit submission
///   GET  /admin/notes/delete/{id} - Show delete confirmation
///   POST /admin/notes/delete/{id} - Handle delete submission
/// 
/// Authorization:
///   All actions require [Authorize(Roles = "Admin")]
///   Non-admin users get 403 Forbidden
///   Anonymous users get redirected to login
/// </remarks>
[Authorize(Roles = "Admin")]
[Route("admin/notes")]
public class AdminNotesController : Controller
{
    private readonly HamcoDbContext _context;
    private readonly ISloganRandomizer _sloganRandomizer;
    private readonly IImageRandomizer _imageRandomizer;

    /// <summary>
    /// Initializes a new instance of the AdminNotesController.
    /// </summary>
    /// <param name="context">Database context for note operations.</param>
    /// <param name="sloganRandomizer">Service for random slogans.</param>
    /// <param name="imageRandomizer">Service for random header images.</param>
    public AdminNotesController(
        HamcoDbContext context,
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer)
    {
        _context = context;
        _sloganRandomizer = sloganRandomizer;
        _imageRandomizer = imageRandomizer;
    }

    /// <summary>
    /// Sets ViewBag properties for layout (slogan and random image).
    /// Called at the start of each action.
    /// </summary>
    private async Task SetViewBagPropertiesAsync()
    {
        ViewBag.Slogan = await _sloganRandomizer.GetRandomSloganAsync();
        ViewBag.RandomImage = _imageRandomizer.GetRandomImage();
        if (ViewBag.Heading == null)
        {
            ViewBag.Heading = "Hamco Internet Solutions";
        }
    }

    /// <summary>
    /// GET /admin/notes - Display list of all notes
    /// </summary>
    /// <returns>View with list of notes.</returns>
    [HttpGet("", Name = "AdminNotesList")]
    public async Task<IActionResult> Index()
    {
        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Admin Notes";
        
        var notes = await _context.Notes
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View(notes);
    }

    /// <summary>
    /// GET /admin/notes/create - Display create note form
    /// </summary>
    /// <returns>View with empty form.</returns>
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Create New Note";
        return View();
    }

    /// <summary>
    /// POST /admin/notes/create - Handle note creation
    /// </summary>
    /// <param name="model">Note creation data from form.</param>
    /// <returns>Redirect to index on success, or form with errors.</returns>
    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateNoteViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await SetViewBagPropertiesAsync();
            ViewBag.Heading = "Create New Note";
            return View(model);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var note = new Note
        {
            Title = model.Title.Trim(),
            Slug = string.IsNullOrWhiteSpace(model.Slug) 
                ? SlugGenerator.GenerateSlug(model.Title) 
                : SlugGenerator.GenerateSlug(model.Slug),
            Content = model.Content.Trim(),
            UserId = userId!,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET /admin/notes/edit/{id} - Display edit note form
    /// </summary>
    /// <param name="id">Note ID to edit.</param>
    /// <returns>View with pre-populated form, or 404 if not found.</returns>
    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound();
        }

        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Edit Note";

        var model = new EditNoteViewModel
        {
            Id = note.Id,
            Title = note.Title,
            Slug = note.Slug,
            Content = note.Content
        };

        return View(model);
    }

    /// <summary>
    /// POST /admin/notes/edit/{id} - Handle note update
    /// </summary>
    /// <param name="id">Note ID to update.</param>
    /// <param name="model">Updated note data from form.</param>
    /// <returns>Redirect to index on success, or form with errors.</returns>
    [HttpPost("edit/{id}")]
    public async Task<IActionResult> Edit(int id, EditNoteViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await SetViewBagPropertiesAsync();
            ViewBag.Heading = "Edit Note";
            return View(model);
        }

        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound();
        }

        note.Title = model.Title.Trim();
        note.Slug = string.IsNullOrWhiteSpace(model.Slug)
            ? SlugGenerator.GenerateSlug(model.Title)
            : SlugGenerator.GenerateSlug(model.Slug);
        note.Content = model.Content.Trim();
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET /admin/notes/delete/{id} - Display delete confirmation
    /// </summary>
    /// <param name="id">Note ID to delete.</param>
    /// <returns>View with note details, or 404 if not found.</returns>
    [HttpGet("delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound();
        }

        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Delete Note";
        return View(note);
    }

    /// <summary>
    /// POST /admin/notes/delete/{id} - Handle note deletion
    /// </summary>
    /// <param name="id">Note ID to delete.</param>
    /// <returns>Redirect to index on success, or 404 if not found.</returns>
    [HttpPost("delete/{id}")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return NotFound();
        }

        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// View model for creating a new note.
/// </summary>
public class CreateNoteViewModel
{
    /// <summary>
    /// Note title (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Title is required")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug (auto-generated from title if empty).
    /// </summary>
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Slug cannot exceed 200 characters")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Note content in Markdown format (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// View model for editing an existing note.
/// </summary>
public class EditNoteViewModel
{
    /// <summary>
    /// Note ID (hidden field).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Note title (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Title is required")]
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug (auto-generated from title if empty).
    /// </summary>
    [System.ComponentModel.DataAnnotations.StringLength(200, ErrorMessage = "Slug cannot exceed 200 characters")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Note content in Markdown format (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
}
