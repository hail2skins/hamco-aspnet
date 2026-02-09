using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Data;
using Hamco.Services;

namespace Hamco.Api.Controllers.Admin;

/// <summary>
/// MVC controller for admin slogans management (CRUD pages).
/// Provides browser-based interface for admins to manage slogans.
/// </summary>
/// <remarks>
/// Routes:
///   GET  /admin/slogans              - List all slogans
///   GET  /admin/slogans/create       - Show create form
///   POST /admin/slogans/create       - Handle create submission
///   GET  /admin/slogans/edit/{id}    - Show edit form
///   POST /admin/slogans/edit/{id}    - Handle edit submission
///   GET  /admin/slogans/delete/{id}  - Show delete confirmation
///   POST /admin/slogans/delete/{id}  - Handle delete submission
///   POST /admin/slogans/toggle/{id}  - Toggle IsActive status
/// 
/// Authorization:
///   All actions require [Authorize(Roles = "Admin")]
///   Non-admin users get 403 Forbidden
///   Anonymous users get 401 Unauthorized
/// </remarks>
[Authorize(Roles = "Admin")]
[Route("admin/slogans")]
public class AdminSlogansController : BaseController
{
    private readonly HamcoDbContext _context;

    /// <summary>
    /// Initializes a new instance of the AdminSlogansController.
    /// </summary>
    /// <param name="context">Database context for slogan operations.</param>
    /// <param name="sloganRandomizer">Service for random slogans.</param>
    /// <param name="imageRandomizer">Service for random header images.</param>
    public AdminSlogansController(
        HamcoDbContext context,
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer)
        : base(sloganRandomizer, imageRandomizer)
    {
        _context = context;
    }

    /// <summary>
    /// GET /admin/slogans - Display list of all slogans
    /// </summary>
    /// <returns>View with list of slogans.</returns>
    [HttpGet("", Name = "AdminSlogansList")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Heading = "Admin Slogans";
        
        var slogans = await _context.Slogans
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View(slogans);
    }

    /// <summary>
    /// GET /admin/slogans/create - Display create slogan form
    /// </summary>
    /// <returns>View with empty form.</returns>
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Heading = "Create New Slogan";
        return View();
    }

    /// <summary>
    /// POST /admin/slogans/create - Handle slogan creation
    /// </summary>
    /// <param name="model">Slogan creation data from form.</param>
    /// <returns>Redirect to index on success, or form with errors.</returns>
    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateSloganViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Heading = "Create New Slogan";
            return View(model);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var slogan = new Slogan
        {
            Text = model.Content.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.Slogans.Add(slogan);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET /admin/slogans/edit/{id} - Display edit slogan form
    /// </summary>
    /// <param name="id">Slogan ID to edit.</param>
    /// <returns>View with pre-populated form, or 404 if not found.</returns>
    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound();
        }

        ViewBag.Heading = "Edit Slogan";

        var model = new EditSloganViewModel
        {
            Id = slogan.Id,
            Content = slogan.Text,
            IsActive = slogan.IsActive
        };

        return View(model);
    }

    /// <summary>
    /// POST /admin/slogans/edit/{id} - Handle slogan update
    /// </summary>
    /// <param name="id">Slogan ID to update.</param>
    /// <param name="model">Updated slogan data from form.</param>
    /// <returns>Redirect to index on success, or form with errors.</returns>
    [HttpPost("edit/{id}")]
    public async Task<IActionResult> Edit(int id, EditSloganViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Heading = "Edit Slogan";
            return View(model);
        }

        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound();
        }

        slogan.Text = model.Content.Trim();
        slogan.IsActive = model.IsActive;
        slogan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET /admin/slogans/delete/{id} - Display delete confirmation
    /// </summary>
    /// <param name="id">Slogan ID to delete.</param>
    /// <returns>View with slogan details, or 404 if not found.</returns>
    [HttpGet("delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound();
        }

        ViewBag.Heading = "Delete Slogan";
        return View(slogan);
    }

    /// <summary>
    /// POST /admin/slogans/delete/{id} - Handle slogan deletion
    /// </summary>
    /// <param name="id">Slogan ID to delete.</param>
    /// <returns>Redirect to index on success, or 404 if not found.</returns>
    [HttpPost("delete/{id}")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound();
        }

        _context.Slogans.Remove(slogan);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// POST /admin/slogans/toggle/{id} - Toggle IsActive status
    /// </summary>
    /// <param name="id">Slogan ID to toggle.</param>
    /// <returns>Redirect to index on success, or 404 if not found.</returns>
    [HttpPost("toggle/{id}")]
    public async Task<IActionResult> Toggle(int id)
    {
        var slogan = await _context.Slogans.FindAsync(id);
        if (slogan == null)
        {
            return NotFound();
        }

        slogan.IsActive = !slogan.IsActive;
        slogan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// View model for creating a new slogan.
/// </summary>
public class CreateSloganViewModel
{
    /// <summary>
    /// Slogan content text (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Content is required")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Content cannot exceed 500 characters")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether the slogan is active. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// View model for editing an existing slogan.
/// </summary>
public class EditSloganViewModel
{
    /// <summary>
    /// Slogan ID (hidden field).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Slogan content text (required).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Content is required")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Content cannot exceed 500 characters")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether the slogan is active.
    /// </summary>
    public bool IsActive { get; set; }
}
