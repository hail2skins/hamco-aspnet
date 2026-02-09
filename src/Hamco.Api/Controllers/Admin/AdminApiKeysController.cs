using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hamco.Core.Models;
using Hamco.Core.Services;
using Hamco.Data;
using Hamco.Services;

namespace Hamco.Api.Controllers.Admin;

/// <summary>
/// MVC controller for admin API key management (browser-based CRUD).
/// Provides interface for admins to generate, view, and revoke API keys.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/apikeys")]
public class AdminApiKeysController : Controller
{
    private readonly IApiKeyService _apiKeyService;
    private readonly HamcoDbContext _context;
    private readonly ISloganRandomizer _sloganRandomizer;
    private readonly IImageRandomizer _imageRandomizer;

    public AdminApiKeysController(
        IApiKeyService apiKeyService,
        HamcoDbContext context,
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer)
    {
        _apiKeyService = apiKeyService;
        _context = context;
        _sloganRandomizer = sloganRandomizer;
        _imageRandomizer = imageRandomizer;
    }

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
    /// GET /admin/apikeys - List all API keys
    /// </summary>
    [HttpGet("", Name = "AdminApiKeysList")]
    public async Task<IActionResult> Index()
    {
        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "API Key Management";

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var keys = await _context.ApiKeys
            .Where(k => k.CreatedByUserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();

        return View(keys);
    }

    /// <summary>
    /// GET /admin/apikeys/create - Show create form
    /// </summary>
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Create New API Key";
        return View();
    }

    /// <summary>
    /// POST /admin/apikeys/create - Generate new API key
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateApiKeyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await SetViewBagPropertiesAsync();
            ViewBag.Heading = "Create New API Key";
            return View(model);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var (key, entity) = await _apiKeyService.GenerateKeyAsync(
            model.Name,
            model.IsAdmin,
            userId
        );

        // Store the plaintext key in TempData to display ONCE
        TempData["NewApiKey"] = key;
        TempData["NewApiKeyName"] = entity.Name;
        TempData["NewApiKeyId"] = entity.Id;

        return RedirectToAction("ShowKey", new { id = entity.Id });
    }

    /// <summary>
    /// GET /admin/apikeys/show/{id} - Display newly created key (ONE TIME ONLY)
    /// </summary>
    [HttpGet("show/{id}")]
    public async Task<IActionResult> ShowKey(string id)
    {
        await SetViewBagPropertiesAsync();
        ViewBag.Heading = "Save Your API Key";

        // Only show if we have the key in TempData
        if (TempData["NewApiKey"] == null || TempData["NewApiKeyId"]?.ToString() != id)
        {
            // Key already shown or invalid access - redirect to list
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ApiKey = TempData["NewApiKey"]!.ToString();
        ViewBag.KeyName = TempData["NewApiKeyName"]?.ToString();
        ViewBag.KeyId = id;

        // Keep TempData for this request only
        TempData.Keep("NewApiKey");
        TempData.Keep("NewApiKeyName");
        TempData.Keep("NewApiKeyId");

        return View();
    }

    /// <summary>
    /// POST /admin/apikeys/revoke/{id} - Revoke an API key
    /// </summary>
    [HttpPost("revoke/{id}")]
    public async Task<IActionResult> Revoke(string id)
    {
        try
        {
            await _apiKeyService.RevokeKeyAsync(id);
            TempData["SuccessMessage"] = "API key revoked successfully.";
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] = "API key not found.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// POST /admin/apikeys/delete/{id} - Delete an API key permanently
    /// </summary>
    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var key = await _context.ApiKeys.FindAsync(id);
        if (key == null)
        {
            TempData["ErrorMessage"] = "API key not found.";
            return RedirectToAction(nameof(Index));
        }

        _context.ApiKeys.Remove(key);
        await _context.SaveChangesAsync();
        
        TempData["SuccessMessage"] = "API key deleted permanently.";
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// View model for creating a new API key
/// </summary>
public class CreateApiKeyViewModel
{
    /// <summary>
    /// Human-readable name for the API key
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Name is required")]
    [System.ComponentModel.DataAnnotations.StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the key should have Admin permissions
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "Admin Access")]
    public bool IsAdmin { get; set; }
}
