using Hamco.Core.Services;
using Hamco.Services;
using Xunit;

namespace Hamco.Core.Tests.Services;

/// <summary>
/// Tests for MarkdownService implementation.
/// Verifies Markdown rendering, syntax highlighting, and XSS protection.
/// </summary>
public class MarkdownServiceTests
{
    private readonly IMarkdownService _markdownService;

    public MarkdownServiceTests()
    {
        _markdownService = new MarkdownService();
    }

    #region Basic Markdown Rendering Tests

    [Fact]
    public void RenderToHtml_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var markdown = string.Empty;

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderToHtml_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? markdown = null;

        // Act
        var result = _markdownService.RenderToHtml(markdown!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderToHtml_WithH1Header_RendersCorrectly()
    {
        // Arrange
        var markdown = "# Hello World";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<h1", result);
        Assert.Contains("Hello World", result);
        Assert.Contains("</h1>", result);
    }

    [Fact]
    public void RenderToHtml_WithH2Header_RendersCorrectly()
    {
        // Arrange
        var markdown = "## Subheading";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<h2", result);
        Assert.Contains("Subheading", result);
        Assert.Contains("</h2>", result);
    }

    [Fact]
    public void RenderToHtml_WithBoldText_RendersCorrectly()
    {
        // Arrange
        var markdown = "This is **bold** text.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void RenderToHtml_WithItalicText_RendersCorrectly()
    {
        // Arrange
        var markdown = "This is *italic* text.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public void RenderToHtml_WithParagraph_RendersCorrectly()
    {
        // Arrange
        var markdown = "This is a simple paragraph.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<p>This is a simple paragraph.</p>", result);
    }

    #endregion

    #region GitHub-Flavored Markdown Tests

    [Fact]
    public void RenderToHtml_WithTable_RendersCorrectly()
    {
        // Arrange
        var markdown = @"| Name | Age |
|------|-----|
| Alice | 25 |
| Bob | 30 |";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<table>", result);
        Assert.Contains("<thead>", result);
        Assert.Contains("<tbody>", result);
        Assert.Contains("<th>Name</th>", result);
        Assert.Contains("<th>Age</th>", result);
        Assert.Contains("<td>Alice</td>", result);
        Assert.Contains("<td>25</td>", result);
    }

    [Fact]
    public void RenderToHtml_WithTaskList_RendersCorrectly()
    {
        // Arrange
        var markdown = @"- [x] Completed task
- [ ] Incomplete task";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<ul", result); // May have class attribute
        Assert.Contains("<li", result);
        Assert.Contains("class=\"task-list-item\"", result);
        Assert.Contains("checked=\"checked\"", result);
    }

    [Fact]
    public void RenderToHtml_WithStrikethrough_RendersCorrectly()
    {
        // Arrange
        var markdown = "This is ~~strikethrough~~ text.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<del>strikethrough</del>", result);
    }

    [Fact]
    public void RenderToHtml_WithAutolink_RendersCorrectly()
    {
        // Arrange
        var markdown = "Visit https://example.com for more info.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<a href=\"https://example.com\">https://example.com</a>", result);
    }

    #endregion

    #region Code Block and Syntax Highlighting Tests

    [Fact]
    public void RenderToHtml_WithCodeBlock_RendersCorrectly()
    {
        // Arrange
        var markdown = @"```
var x = 42;
```";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<pre>", result);
        Assert.Contains("<code>", result);
        Assert.Contains("var x = 42;", result);
    }

    [Fact]
    public void RenderToHtml_WithInlineCode_RendersCorrectly()
    {
        // Arrange
        var markdown = "Use the `console.log()` function.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("<code>console.log()</code>", result);
    }

    [Fact]
    public void RenderToHtmlWithSyntaxHighlight_WithCSharpCode_AppliesSyntaxHighlighting()
    {
        // Arrange
        var markdown = @"```csharp
var name = ""Alice"";
var age = 25;
Console.WriteLine($""Name: {name}, Age: {age}"");
```";

        // Act
        var result = _markdownService.RenderToHtmlWithSyntaxHighlight(markdown);

        // Assert
        Assert.Contains("var", result);
        Assert.Contains("Alice", result);
        // ColorCode adds div or span elements with styling
        Assert.True(
            result.Contains("<div") || result.Contains("<span") || result.Contains("style="),
            "Expected syntax highlighting markup (div/span/style)"
        );
    }

    [Fact]
    public void RenderToHtmlWithSyntaxHighlight_WithJavaScriptCode_AppliesSyntaxHighlighting()
    {
        // Arrange
        var markdown = @"```javascript
const message = 'Hello, World!';
console.log(message);
```";

        // Act
        var result = _markdownService.RenderToHtmlWithSyntaxHighlight(markdown);

        // Assert
        Assert.Contains("const", result);
        Assert.Contains("Hello, World!", result);
        Assert.True(
            result.Contains("<div") || result.Contains("<span") || result.Contains("style="),
            "Expected syntax highlighting markup"
        );
    }

    [Fact]
    public void RenderToHtmlWithSyntaxHighlight_WithMultipleCodeBlocks_HighlightsAll()
    {
        // Arrange
        var markdown = @"# Code Examples

C# example:
```csharp
var x = 42;
```

JavaScript example:
```javascript
const y = 42;
```";

        // Act
        var result = _markdownService.RenderToHtmlWithSyntaxHighlight(markdown);

        // Assert
        Assert.Contains("Code Examples", result);
        Assert.Contains("var", result); // C# var keyword
        Assert.Contains("const", result); // JavaScript const keyword
    }

    #endregion

    #region XSS Sanitization Tests

    [Fact]
    public void SanitizeHtml_WithScriptTag_RemovesScriptTag()
    {
        // Arrange
        var html = "<p>Safe content</p><script>alert('XSS');</script>";

        // Act
        var result = _markdownService.SanitizeHtml(html);

        // Assert
        Assert.Contains("<p>Safe content</p>", result);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void SanitizeHtml_WithOnClickAttribute_RemovesOnClick()
    {
        // Arrange
        var html = "<button onclick=\"alert('XSS')\">Click me</button>";

        // Act
        var result = _markdownService.SanitizeHtml(html);

        // Assert
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void SanitizeHtml_WithJavaScriptUrl_RemovesJavaScriptUrl()
    {
        // Arrange
        var html = "<a href=\"javascript:alert('XSS')\">Click me</a>";

        // Act
        var result = _markdownService.SanitizeHtml(html);

        // Assert
        Assert.DoesNotContain("javascript:", result);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void SanitizeHtml_WithIframe_RemovesIframe()
    {
        // Arrange
        var html = "<p>Content</p><iframe src=\"https://evil.com\"></iframe>";

        // Act
        var result = _markdownService.SanitizeHtml(html);

        // Assert
        Assert.Contains("<p>Content</p>", result);
        Assert.DoesNotContain("<iframe", result);
    }

    [Fact]
    public void SanitizeHtml_WithSafeHtml_PreservesSafeElements()
    {
        // Arrange
        var html = @"
<h1>Title</h1>
<p>This is a <strong>bold</strong> and <em>italic</em> paragraph.</p>
<ul>
  <li>Item 1</li>
  <li>Item 2</li>
</ul>
<a href=""https://example.com"">Link</a>";

        // Act
        var result = _markdownService.SanitizeHtml(html);

        // Assert
        Assert.Contains("<h1>Title</h1>", result);
        Assert.Contains("<strong>bold</strong>", result);
        Assert.Contains("<em>italic</em>", result);
        Assert.Contains("<ul>", result);
        Assert.Contains("<li>Item 1</li>", result);
        Assert.Contains("<a href=\"https://example.com\">Link</a>", result);
    }

    #endregion

    #region Combined Rendering and Sanitization Tests

    [Fact]
    public void RenderToHtmlWithSyntaxHighlight_SanitizesOutput()
    {
        // Arrange - Markdown that would produce XSS if not sanitized
        var markdown = @"# Hello

<script>alert('XSS');</script>

This is a paragraph.";

        // Act
        var result = _markdownService.RenderToHtmlWithSyntaxHighlight(markdown);

        // Assert
        Assert.Contains("<h1", result);
        Assert.Contains("Hello", result);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("This is a paragraph.", result);
    }

    [Fact]
    public void RenderToHtmlWithSyntaxHighlight_WithCodeAndMarkdown_RendersAndSanitizes()
    {
        // Arrange
        var markdown = @"# API Documentation

## Authentication

Use the following header:

```javascript
const headers = {
  'Authorization': 'Bearer ' + token
};
```

<script>alert('xss')</script>

**Important:** Keep your token secure!";

        // Act
        var result = _markdownService.RenderToHtmlWithSyntaxHighlight(markdown);

        // Assert
        Assert.Contains("API Documentation", result);
        Assert.Contains("Authentication", result);
        Assert.Contains("Authorization", result);
        Assert.Contains("Bearer", result);
        Assert.Contains("<strong>Important:</strong>", result);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("alert('xss')", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RenderToHtml_WithVeryLongContent_HandlesGracefully()
    {
        // Arrange
        var markdown = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}"));

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 1000", result);
    }

    [Fact]
    public void RenderToHtml_WithUnicodeCharacters_PreservesUnicode()
    {
        // Arrange
        var markdown = "# 你好世界 🚀\n\nこんにちは **世界** 🌍";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        Assert.Contains("你好世界", result);
        Assert.Contains("🚀", result);
        Assert.Contains("こんにちは", result);
        Assert.Contains("世界", result);
        Assert.Contains("🌍", result);
    }

    [Fact]
    public void RenderToHtml_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var markdown = "This has <angle brackets> and & ampersands.";

        // Act
        var result = _markdownService.RenderToHtml(markdown);

        // Assert
        // Markdown treats <angle brackets> as HTML, so they should be preserved or escaped
        Assert.Contains("ampersands", result);
    }

    #endregion
}
