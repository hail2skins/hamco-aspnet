using Ganss.Xss;
using Hamco.Core.Services;
using Markdig;
using Markdown.ColorCode;

namespace Hamco.Services;

/// <summary>
/// Implementation of IMarkdownService using Markdig with GitHub-flavored Markdown,
/// Markdown.ColorCode for syntax highlighting, and HtmlSanitizer for XSS protection.
/// </summary>
public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly MarkdownPipeline _pipelineWithSyntaxHighlighting;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownService()
    {
        // Configure basic GitHub-flavored Markdown pipeline
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Includes tables, task lists, autolinks, etc.
            .Build();

        // Configure pipeline with syntax highlighting using ColorCode
        _pipelineWithSyntaxHighlighting = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseColorCode() // Adds Markdown.ColorCode extension for syntax highlighting
            .Build();

        // Configure HTML sanitizer to allow safe HTML elements
        _sanitizer = new HtmlSanitizer();
        ConfigureSanitizer();
    }

    /// <summary>
    /// Configures the HTML sanitizer with allowed tags and attributes.
    /// Removes dangerous elements while preserving formatting and code blocks.
    /// </summary>
    private void ConfigureSanitizer()
    {
        // Allow common formatting tags
        _sanitizer.AllowedTags.Add("div");
        _sanitizer.AllowedTags.Add("span");
        _sanitizer.AllowedTags.Add("pre");
        _sanitizer.AllowedTags.Add("code");
        _sanitizer.AllowedTags.Add("h1");
        _sanitizer.AllowedTags.Add("h2");
        _sanitizer.AllowedTags.Add("h3");
        _sanitizer.AllowedTags.Add("h4");
        _sanitizer.AllowedTags.Add("h5");
        _sanitizer.AllowedTags.Add("h6");
        _sanitizer.AllowedTags.Add("p");
        _sanitizer.AllowedTags.Add("br");
        _sanitizer.AllowedTags.Add("hr");
        _sanitizer.AllowedTags.Add("strong");
        _sanitizer.AllowedTags.Add("em");
        _sanitizer.AllowedTags.Add("ul");
        _sanitizer.AllowedTags.Add("ol");
        _sanitizer.AllowedTags.Add("li");
        _sanitizer.AllowedTags.Add("blockquote");
        _sanitizer.AllowedTags.Add("a");
        _sanitizer.AllowedTags.Add("table");
        _sanitizer.AllowedTags.Add("thead");
        _sanitizer.AllowedTags.Add("tbody");
        _sanitizer.AllowedTags.Add("tr");
        _sanitizer.AllowedTags.Add("th");
        _sanitizer.AllowedTags.Add("td");
        _sanitizer.AllowedTags.Add("del");
        _sanitizer.AllowedTags.Add("ins");
        _sanitizer.AllowedTags.Add("sup");
        _sanitizer.AllowedTags.Add("sub");

        // Allow common attributes for styling and functionality
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("id");
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");
        _sanitizer.AllowedAttributes.Add("style");
        _sanitizer.AllowedAttributes.Add("rel");
        _sanitizer.AllowedAttributes.Add("target");
        _sanitizer.AllowedAttributes.Add("align");

        // Allow data attributes for syntax highlighting
        _sanitizer.AllowDataAttributes = true;

        // Ensure links are safe
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        // Allow CSS properties for syntax highlighting
        _sanitizer.AllowedCssProperties.Add("color");
        _sanitizer.AllowedCssProperties.Add("background-color");
        _sanitizer.AllowedCssProperties.Add("font-weight");
        _sanitizer.AllowedCssProperties.Add("font-style");
        _sanitizer.AllowedCssProperties.Add("text-decoration");
    }

    /// <inheritdoc />
    public string RenderToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }

    /// <inheritdoc />
    public string RenderToHtmlWithSyntaxHighlight(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        // Render with syntax highlighting
        var html = Markdig.Markdown.ToHtml(markdown, _pipelineWithSyntaxHighlighting);

        // Sanitize to remove XSS vectors
        return SanitizeHtml(html);
    }

    /// <inheritdoc />
    public string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }

    /// <inheritdoc />
    public string ToPlainText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        // First render markdown to HTML
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);

        // Strip HTML tags to get plain text
        var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);

        // Decode HTML entities (&amp; -> &, &lt; -> <, etc.)
        plainText = System.Net.WebUtility.HtmlDecode(plainText);

        // Normalize whitespace (collapse multiple spaces/newlines)
        plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ").Trim();

        return plainText;
    }
}
