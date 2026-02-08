using Hamco.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hamco.Api.Controllers;

/// <summary>
/// Controller for home and static pages.
/// </summary>
public class HomeController : BaseController
{
    public HomeController(ISloganRandomizer sloganRandomizer, IImageRandomizer imageRandomizer)
        : base(sloganRandomizer, imageRandomizer)
    {
    }

    /// <summary>
    /// Home page (index).
    /// </summary>
    [HttpGet("/")]
    public IActionResult Index()
    {
        ViewBag.Title = "Home";
        ViewBag.Heading = "Hamco Internet Solutions";
        return View();
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
