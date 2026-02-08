namespace Hamco.Services;

/// <summary>
/// Service for randomly selecting header background images.
/// </summary>
public interface IImageRandomizer
{
    /// <summary>
    /// Gets a random header image path.
    /// </summary>
    /// <returns>Relative path to a random header image.</returns>
    string GetRandomImage();
}

/// <summary>
/// Implementation of the image randomizer service.
/// </summary>
public class ImageRandomizer : IImageRandomizer
{
    private static readonly string[] HeaderImages = new[]
    {
        "/img/main/hammy1.png",
        "/img/main/hammy2.png",
        "/img/main/hammy3.png",
        "/img/main/hammy4.png"
    };

    private static readonly Random _random = new();

    /// <inheritdoc />
    public string GetRandomImage()
    {
        var index = _random.Next(HeaderImages.Length);
        return HeaderImages[index];
    }
}
