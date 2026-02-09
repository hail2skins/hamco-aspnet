using Hamco.Services;
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

    public HomeController(
        ISloganRandomizer sloganRandomizer,
        IImageRandomizer imageRandomizer,
        HamcoDbContext context)
        : base(sloganRandomizer, imageRandomizer)
    {
        _context = context;
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
