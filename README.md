# xperience-community-seo

A centralized repository dedicated to essential SEO infrastructure files like robots.txt, sitemap.xml, llms.txt, and more. It aims to provide optimized configuration files that enhance search engine crawling, indexing, and visibility for websites and AI-driven search models.

## Features

- **Configurable Sitemap Endpoint**: Generate XML sitemaps with a customizable URL path
- **Dynamic Content Discovery**: Automatically discover and include content items based on your configuration
- **Cache Optimization**: Built-in caching using Kentico's cache dependency system
- **Flexible Configuration**: Configure which content types, fields, and languages to include
- **Markdown Content Negotiation**: Serve Markdown renditions of HTML pages for agents and LLM crawlers via `Accept: text/markdown`

## Quick Start

### Installation

Install the NuGet package:

```bash
dotnet add package XperienceCommunity.SEO
```

### Configuration

Register the SEO services in your `Program.cs`:

```csharp
using XperienceCommunity.SEO;

var builder = WebApplication.CreateBuilder(args);

// Register the SEO services with configuration
builder.Services.AddWebsiteDiscoveryProvider(options =>
{
    options.ReusableSchemaName = "PageMetadata"; // Your reusable schema name
    options.DefaultLanguage = "en-US";
    options.DescriptionFieldName = "MetaDescription";
    options.TitleFieldName = "MetaTitle";
    options.SitemapShowFieldName = "ShowInSitemap"; // Optional field
    options.SitemapPriorityFieldName = "SitemapPriority"; // Optional field, a value from 0.0 to 1.0
    options.SitemapFrequencyFieldName = "SitemapFrequency"; // Optional field, e.g. "daily" or "monthly"
    options.LlmsTxtTitles["MyWebsiteChannel"] = "My Website"; // Optional llms.txt heading per website channel
    options.ContentTypeDependencies = new[] 
    { 
        "BlogPost", // or BlogPost.CONTENT_TYPE_NAME
        "Article", 
        "LandingPage" 
    };
});

var app = builder.Build();

// Enable Markdown content negotiation (place before UseRouting/MapControllers)
app.UseMarkdownContentNegotiation();

// Your middleware configuration...
app.MapControllers();

app.Run();
```

### How pages are listed

- **Per website channel.** The sitemap and llms.txt list the pages of the channel that serves the request, so one application can host several sites. Results are cached per channel and language.
- **Refreshed on publish.** The cache depends on the configured content types in the channel, so publishing or unpublishing a page updates the output straight away. Otherwise it expires after 60 minutes.
- **Canonical URLs.** Each page is listed at its canonical URL, from `IWebPageUrlRetriever`: the site root for the home page, and the vanity URL when one is selected as canonical.
- **Last modified.** `lastmod` is the date the page was last published, with the server's time zone offset.
- **Priority and change frequency** come from the optional `SitemapPriorityFieldName` and `SitemapFrequencyFieldName` fields. Without a priority, none is listed; without a frequency, `weekly` is listed.
- **llms.txt titles.** A page without a title uses the name editors give it in the content tree. The heading is the channel's entry in `LlmsTxtTitles`, or the channel name.

## Usage Examples

### 1. Basic Controller Example

```csharp
using Microsoft.AspNetCore.Mvc;
using XperienceCommunity.SEO.Services;

[ApiController]
public class SEOController : ControllerBase
{
    private readonly IWebsiteDiscoveryProvider _websiteDiscoveryProvider;

    public SEOController(IWebsiteDiscoveryProvider websiteDiscoveryProvider)
    {
        _websiteDiscoveryProvider = websiteDiscoveryProvider;
    }

    // Generates sitemap.xml at /sitemap.xml
    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<ActionResult> GetSitemap()
    {
        return await _websiteDiscoveryProvider.GenerateSitemap();
    }

    // Generates llms.txt at /llms.txt
    [HttpGet("/llms.txt")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<ActionResult> GetLlmsTxt()
    {
        return await _websiteDiscoveryProvider.GenerateLlmsTxt();
    }

    // Generates robots.txt at /robots.txt
    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 86400)] // Cache for 24 hours
    public ActionResult GetRobotsTxt()
    {
        return Content(_websiteDiscoveryProvider.GenerateRobotsTxt(), "text/plain; charset=utf-8");
    }
}
```

### 2. Using Minimal APIs

```csharp
app.MapGet("/sitemap.xml", async (IWebsiteDiscoveryProvider provider, HttpContext context) =>
{
    var actionResult = await provider.GenerateSitemap();
    await actionResult.ExecuteResultAsync(new ActionContext
    {
        HttpContext = context
    });
});

app.MapGet("/llms.txt", async (IWebsiteDiscoveryProvider provider, HttpContext context) =>
{
    var actionResult = await provider.GenerateLlmsTxt();
    await actionResult.ExecuteResultAsync(new ActionContext
    {
        HttpContext = context
    });
});

app.MapGet("/robots.txt", (IWebsiteDiscoveryProvider provider, HttpContext context) =>
{
    var robotsContent = provider.GenerateRobotsTxt();
    return Results.Content(robotsContent, "text/plain; charset=utf-8");
});
```

### 3. Using Route Attributes

```csharp
[Route("seo")]
public class SEOController : ControllerBase
{
    private readonly IWebsiteDiscoveryProvider _provider;

    public SEOController(IWebsiteDiscoveryProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("~/sitemap.xml")] // ~/ makes it root-relative
    public async Task<ActionResult> Sitemap() 
        => await _provider.GenerateSitemap();

    [HttpGet("~/llms.txt")] // ~/ makes it root-relative
    public async Task<ActionResult> LlmsTxt() 
        => await _provider.GenerateLlmsTxt();

    [HttpGet("~/robots.txt")] // ~/ makes it root-relative
    public ActionResult RobotsTxt() 
        => Content(_provider.GenerateRobotsTxt(), "text/plain; charset=utf-8");
}
```

## Configuration for robots.txt

Add to your `appsettings.json`:

```json
{
  "XperienceCommunitySEO": {
    "RobotsContent": "User-agent: Twitterbot\nDisallow:\n\nUser-agent: SiteAuditBot\nAllow: /\n\nUser-agent: *\nDisallow: /"
  }
}
```

For production, you could use the following sample:

```json
{
  "XperienceCommunitySEO": {
    "RobotsContent": "User-agent: *\nAllow: /"
  }
}
```

## Expected Output

### robots.txt (Non-production)
```
User-agent: Twitterbot
Disallow:

User-agent: SiteAuditBot
Allow: /

User-agent: *
Disallow: /
```

### robots.txt (Production)
```
User-agent: *
Allow: /
```

### sitemap.xml
```xml
<?xml version="1.0" encoding="utf-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://yoursite.com/</loc>
    <lastmod>2025-10-03T14:20:11.5300000-05:00</lastmod>
    <changefreq>daily</changefreq>
    <priority>0.8</priority>
  </url>
  <url>
    <loc>https://yoursite.com/about</loc>
    <lastmod>2025-09-18T09:02:47.1200000-05:00</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.5</priority>
  </url>
</urlset>
```

### llms.txt
```
# YourWebsiteName

## Pages

- [About Us](https://yoursite.com/about): Learn about our company and mission
- [Blog Article](https://yoursite.com/blog/article): Comprehensive guide to SEO
- [Contact](https://yoursite.com/contact): Get in touch with our team
```

## Advanced Usage - Custom Sitemap Generation

The `IWebsiteDiscoveryProvider` service also exposes public methods that allow you to retrieve sitemap data and create custom implementations:

### Available Methods

- **`GetSitemapPages()`** - Returns a list of `SitemapNode` objects for generating XML sitemaps
- **`GetSitemapPagesWithDetails()`** - Returns a list of `SitemapPage` objects with additional metadata like titles and descriptions

### Custom Sitemap Example

```csharp
[ApiController]
public class CustomSEOController : ControllerBase
{
    private readonly IWebsiteDiscoveryProvider _provider;

    public CustomSEOController(IWebsiteDiscoveryProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("/custom-sitemap.xml")]
    public async Task<ActionResult> GetCustomSitemap()
    {
        // Get the basic sitemap nodes
        var sitemapNodes = await _provider.GetSitemapPages();
        
        // Customize the nodes (e.g., add custom change frequency, priority, etc.)
        foreach (var node in sitemapNodes)
        {
            if (node.Url.Contains("/blog/"))
            {
                node.ChangeFrequency = ChangeFrequency.Daily;
                node.Priority = 0.8m;
            }
            else if (node.Url.Contains("/news/"))
            {
                node.ChangeFrequency = ChangeFrequency.Hourly;
                node.Priority = 0.9m;
            }
        }

        // Generate custom sitemap XML
        return new SitemapProvider().CreateSitemap(new SitemapModel(sitemapNodes));
    }

    [HttpGet("/pages-with-metadata.json")]
    public async Task<ActionResult> GetPagesWithMetadata()
    {
        // Get detailed page information including titles and descriptions
        var pagesWithDetails = await _provider.GetSitemapPagesWithDetails();
        
        // Transform or filter the data as needed
        var customData = pagesWithDetails.Select(page => new
        {
            Url = page.Url,
            Title = page.Title,
            Description = page.Description,
            LastModified = page.LastModified
        });

        return Ok(customData);
    }
}
```

### Data Models

**SitemapNode** contains:
- `Url` - The page URL path
- `LastModificationDate` - When the page was last modified
- `ChangeFrequency` - How often the page changes
- `Priority` - Page priority (0.0 to 1.0)

**SitemapPage** contains:
- `SystemFields` - System information about the web page
- `Title` - The page title from your configured title field
- `Description` - The page description from your configured description field
- `IsInSitemap` - Whether the page should be included in sitemaps
- `Url` - The page's canonical URL, relative to the site root (`/` for the home page)
- `DisplayName` - The name editors give the page in the content tree
- `LastModified` - When the page was last published
- `Priority` - The value of the configured priority field, if any
- `ChangeFrequency` - The value of the configured change frequency field, if any

## Markdown Content Negotiation

When `UseMarkdownContentNegotiation()` is registered, any request that sends `Accept: text/markdown` receives a Markdown rendition of the HTML page instead of the default HTML response. HTML → Markdown conversion is powered by [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net), preserving headings, links, lists, images, and tables.

- Response `Content-Type` is set to `text/markdown; charset=utf-8`
- An `x-markdown-tokens` response header contains an estimated token count
- Non-HTML responses (XML, JSON, images, PDFs, etc.) are streamed through unchanged, without buffering
- HTML and Markdown responses carry `Vary: Accept`, so shared caches keep the two renditions apart
- Relative URLs (`/path`, `~/path`) in `src` and `href` attributes are rewritten to absolute URLs using the current request's scheme and host

### Options

`UseMarkdownContentNegotiation` accepts an optional callback to configure `MarkdownNegotiationOptions`:

```csharp
app.UseMarkdownContentNegotiation(opts =>
{
    opts.SkippedPaths  = ["/api/*", "/health"];
    opts.StripElements = ["header", "nav", "footer", "script", "style", "noscript"];
    opts.StripClasses  = ["page-spinner"];
});
```

#### `SkippedPaths`

Paths that should **never** receive Markdown negotiation, even when `Accept: text/markdown` is sent. All other paths are eligible by default.

| Pattern | Behavior |
|---|---|
| `/health` | Exact match only |
| `/api/*` | Matches `/api/` and any path beneath it |

When the array is **empty** (the default), no paths are skipped.

#### `StripElements`

HTML tag names to remove from the page before conversion. Use this to strip layout chrome (navigation, headers, footers, scripts) so agents receive only meaningful page content.

Defaults: `header`, `nav`, `footer`, `script`, `style`, `noscript`

Set to an empty array to disable tag-based stripping entirely:

```csharp
app.UseMarkdownContentNegotiation(opts =>
{
    opts.StripElements = [];
});
```

#### `StripClasses`

CSS class names whose elements should be removed before conversion. Any element that carries one of these classes is removed entirely, regardless of its tag name. Useful for removing decorative wrappers, loading spinners, or empty layout containers that produce noise in the Markdown output.

Defaults: `[]` (disabled)

```csharp
app.UseMarkdownContentNegotiation(opts =>
{
    opts.StripClasses = ["page-spinner"];
});
```

### Testing

```bash
# Default HTML response
curl https://localhost:5001/about-us

# Markdown negotiation
curl -H "Accept: text/markdown" https://localhost:5001/about-us

# Inspect response headers
curl -I -H "Accept: text/markdown" https://localhost:5001/about-us
# Content-Type: text/markdown; charset=utf-8
# x-markdown-tokens: <number>
```

## Testing

You can test the endpoints using curl or your browser:

```bash
# Get robots.txt
curl https://localhost:5001/robots.txt

# Get sitemap
curl https://localhost:5001/sitemap.xml

# Get llms.txt
curl https://localhost:5001/llms.txt
```

## License

MIT License - see [LICENSE](LICENSE) for details. 
