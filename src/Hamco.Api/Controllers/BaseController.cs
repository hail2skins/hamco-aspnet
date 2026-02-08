using Hamco.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hamco.Api.Controllers;

/// <summary>
/// Base controller that provides common functionality for all MVC controllers.
/// Automatically sets ViewBag properties for slogan and random image on every action.
/// </summary>
public abstract class BaseController : Controller
{
    private readonly ISloganRandomizer _sloganRandomizer;
    private readonly IImageRandomizer _imageRandomizer;

    protected BaseController(ISloganRandomizer sloganRandomizer, IImageRandomizer imageRandomizer)
    {
        _sloganRandomizer = sloganRandomizer;
        _imageRandomizer = imageRandomizer;
    }

    /// <summary>
    /// Called before each action executes. Sets ViewBag properties for layout.
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Set random slogan
        ViewBag.Slogan = await _sloganRandomizer.GetRandomSloganAsync();
        
        // Set random header image
        ViewBag.RandomImage = _imageRandomizer.GetRandomImage();

        // Set default heading (controllers can override this)
        if (ViewBag.Heading == null)
        {
            ViewBag.Heading = "Hamco Internet Solutions";
        }

        await base.OnActionExecutionAsync(context, next);
    }
}
