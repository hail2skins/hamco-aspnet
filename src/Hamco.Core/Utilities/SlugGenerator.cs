using System.Text;
using System.Text.RegularExpressions;

namespace Hamco.Core.Utilities;

/// <summary>
/// Utility class for generating URL-friendly slugs from text.
/// Converts titles like "Hello World!" into "hello-world".
/// </summary>
/// <remarks>
/// What is a slug?
///   A URL-friendly version of a string, typically used in URLs.
///   
/// Examples:
///   "Hello World"           → "hello-world"
///   "ASP.NET Core Tips!"    → "aspnet-core-tips"
///   "C# Design Patterns"    → "c-design-patterns"
///   "  Multiple   Spaces  " → "multiple-spaces"
/// 
/// Why use slugs?
///   - SEO: Search engines prefer readable URLs
///   - User-friendly: Easier to remember and share
///   - Professional: /posts/hello-world vs /posts/123
/// 
/// Static class in C#:
///   - Cannot be instantiated (no 'new SlugGenerator()')
///   - Contains only static members
///   - Used for utility/helper functions
///   - Think of it as a namespace for related functions
/// </remarks>
public static class SlugGenerator
{
    /// <summary>
    /// Converts a title string into a URL-friendly slug.
    /// </summary>
    /// <param name="title">The original title to convert.</param>
    /// <returns>A lowercase, hyphenated slug with only alphanumeric characters.</returns>
    /// <remarks>
    /// Algorithm:
    /// 1. Convert to lowercase
    /// 2. Remove special characters (keep a-z, 0-9, spaces, hyphens)
    /// 3. Replace whitespace with hyphens
    /// 4. Remove duplicate hyphens
    /// 5. Trim leading/trailing hyphens
    /// 
    /// Example walkthrough for "Hello World!":
    ///   1. "hello world!"        (lowercase)
    ///   2. "hello world"         (remove '!')
    ///   3. "hello-world"         (spaces → hyphens)
    ///   4. "hello-world"         (no duplicates)
    ///   5. "hello-world"         (no trim needed)
    /// 
    /// Edge cases handled:
    ///   - Empty/null input → returns empty string
    ///   - All special chars → returns empty string
    ///   - Multiple spaces → single hyphen
    ///   - Leading/trailing spaces → trimmed
    /// 
    /// Limitations:
    ///   - Loses Unicode/accented characters (café → caf)
    ///   - No collision handling (two titles → same slug)
    ///   - No length limit enforcement
    /// 
    /// Future improvements:
    ///   - Support Unicode (café → cafe, not caf)
    ///   - Add collision detection (append -2, -3, etc.)
    ///   - Truncate to max length (e.g., 50 chars)
    /// </remarks>
    public static string GenerateSlug(string title)
    {
        // Guard clause: handle null/empty/whitespace input
        // 'string.IsNullOrWhiteSpace()' checks for null, empty, or only whitespace
        // Returns true for: null, "", "   "
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Step 1: Convert to lowercase
        // 'ToLowerInvariant()' converts to lowercase using invariant culture
        // Why not 'ToLower()'? ToLowerInvariant() is culture-independent
        // (Turkish has special lowercase rules, we want consistent behavior)
        var slug = title.ToLowerInvariant();

        // Step 2: Remove special characters (keep only a-z, 0-9, spaces, hyphens)
        // Regex.Replace(input, pattern, replacement)
        //   - [^a-z0-9\s-] means "anything NOT a-z, 0-9, whitespace, or hyphen"
        //   - ^ inside [] means "NOT" (negation)
        //   - \s matches any whitespace (space, tab, newline)
        //   - Replaces all matches with empty string (removes them)
        // Example: "hello! world?" → "hello world"
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

        // Step 3: Replace one or more whitespace characters with a single hyphen
        // \s+ means "one or more whitespace characters"
        // + is a quantifier (one or more)
        // Example: "hello   world" → "hello-world"
        slug = Regex.Replace(slug, @"\s+", "-");

        // Step 4: Replace multiple consecutive hyphens with a single hyphen
        // -+ means "one or more hyphens"
        // This handles cases like "hello--world" → "hello-world"
        // Can occur from: "hello - world" → "hello---world" → "hello-world"
        slug = Regex.Replace(slug, @"-+", "-");

        // Step 5: Remove leading and trailing hyphens
        // 'Trim()' removes whitespace by default
        // 'Trim('-')' removes hyphens from start and end
        // Example: "-hello-world-" → "hello-world"
        slug = slug.Trim('-');

        // Return the final slug
        // At this point, slug is lowercase, alphanumeric + hyphens only
        return slug;
    }
}
