namespace Hamco.Core.Services;

/// <summary>
/// Service for rendering Markdown to HTML with syntax highlighting and XSS protection.
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Renders Markdown text to HTML using GitHub-flavored Markdown.
    /// </summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <returns>HTML string representation of the Markdown.</returns>
    /// <remarks>
    /// This method does NOT sanitize the output. For user-generated content,
    /// use <see cref="RenderToHtmlWithSyntaxHighlight"/> or call <see cref="SanitizeHtml"/> separately.
    /// </remarks>
    /// <example>
    /// <code>
    /// var html = markdownService.RenderToHtml("# Hello World\n\nThis is **bold**.");
    /// // Returns: "&lt;h1&gt;Hello World&lt;/h1&gt;\n&lt;p&gt;This is &lt;strong&gt;bold&lt;/strong&gt;.&lt;/p&gt;"
    /// </code>
    /// </example>
    string RenderToHtml(string markdown);

    /// <summary>
    /// Renders Markdown text to HTML with syntax highlighting for code blocks
    /// and sanitizes the output to remove XSS vectors.
    /// </summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <returns>Sanitized HTML string with syntax-highlighted code blocks.</returns>
    /// <remarks>
    /// Security: This method sanitizes the output using HtmlSanitizer to prevent XSS attacks.
    /// Recommended for rendering user-generated Markdown content.
    /// </remarks>
    /// <example>
    /// <code>
    /// var html = markdownService.RenderToHtmlWithSyntaxHighlight("```csharp\nvar x = 42;\n```");
    /// // Returns: HTML with syntax-highlighted C# code, script tags removed
    /// </code>
    /// </example>
    string RenderToHtmlWithSyntaxHighlight(string markdown);

    /// <summary>
    /// Sanitizes HTML to remove dangerous elements and attributes that could lead to XSS attacks.
    /// </summary>
    /// <param name="html">The HTML to sanitize.</param>
    /// <returns>Sanitized HTML string safe for display.</returns>
    /// <remarks>
    /// Security: Removes script tags, event handlers, and other potentially dangerous HTML.
    /// Allows safe HTML elements like headings, paragraphs, lists, code blocks, etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// var safe = markdownService.SanitizeHtml("&lt;p&gt;Safe&lt;/p&gt;&lt;script&gt;alert('xss')&lt;/script&gt;");
    /// // Returns: "&lt;p&gt;Safe&lt;/p&gt;" (script removed)
    /// </code>
    /// </example>
    string SanitizeHtml(string html);

    /// <summary>
    /// Converts Markdown text to plain text by stripping all Markdown formatting.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>Plain text string with all Markdown syntax removed.</returns>
    /// <remarks>
    /// Useful for generating excerpts or summaries where formatting is not desired.
    /// Removes headers, bold/italic, links, code blocks, etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// var plain = markdownService.ToPlainText("# Hello World\n\nThis is **bold** text.");
    /// // Returns: "Hello World This is bold text."
    /// </code>
    /// </example>
    string ToPlainText(string markdown);
}
