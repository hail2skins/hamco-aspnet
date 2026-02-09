using Hamco.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Hamco.Services;

/// <summary>
/// Service for randomly selecting active slogans from the database.
/// Implements caching for performance optimization.
/// </summary>
public interface ISloganRandomizer
{
    /// <summary>
    /// Gets a random active slogan from the database.
    /// </summary>
    /// <returns>A random slogan text, or a default message if no slogans are available.</returns>
    Task<string> GetRandomSloganAsync();
}

/// <summary>
/// Implementation of the slogan randomizer service.
/// </summary>
public class SloganRandomizer : ISloganRandomizer
{
    private readonly HamcoDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "ActiveSlogans";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly Random _random = new();
    private const string DefaultSlogan = "Your trusted partner in Internet solutions";

    public SloganRandomizer(HamcoDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<string> GetRandomSloganAsync()
    {
        // Fetch fresh from database every time (no caching for true randomness)
        var slogans = await _context.Slogans
            .Where(s => s.IsActive)
            .Select(s => s.Text)
            .ToListAsync();

        // If no slogans found, return default
        if (slogans == null || slogans.Count == 0)
        {
            return DefaultSlogan;
        }

        // Return a random slogan
        var index = _random.Next(slogans.Count);
        return slogans[index];
    }
}
