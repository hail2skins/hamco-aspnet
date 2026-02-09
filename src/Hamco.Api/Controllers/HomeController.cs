using Hamco.Services;
using Hamco.Core.Services;
using Hamco.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hamco.Api.Controllers;

/// <summary>
/// Controller for home and static pages.
/// </summary>
public class HomeController : BaseController
{
    private readonly HamcoDbContext _context;
    private readonly IMarkdownService _markdownService;

    public HomeController(
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer,
        HamcoDbContext context,
        IMarkdownService markdownService)
        : base(sloganRandomizer, imageRandomizer)
    {
        _context = context;
        _markdownService = markdownService;
    }

    /// <summary>
    /// Home page (index).
    /// </summary>
    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Title = "Home";
        ViewBag.Heading = "Hamco Internet Solutions";

        var recentNotes = await _context.Notes
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Prepare plain text excerpts for display (~500 chars for homepage)
        ViewBag.PlainTextExcerpts = recentNotes.ToDictionary(
            n => n.Id,
            n =>
            {
                var plain = _markdownService.ToPlainText(n.Content);
                return plain.Length > 500 ? plain.Substring(0, 500) + "..." : plain;
            }
        );

        return View(recentNotes);
    }

    /// <summary>
    /// About page.
    /// </summary>
    [HttpGet("/about")]
    public IActionResult About()
    {
        ViewBag.Title = "About";
        ViewBag.Heading = "About Hamco";
        return View();
    }
}
