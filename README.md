# xperience-community-seo

A centralized repository dedicated to essential SEO infrastructure files like robots.txt, sitemap.xml, llms.txt, and more. It aims to provide optimized configuration files that enhance search engine crawling, indexing, and visibility for websites and AI-driven search models.

## Features

- **Configurable Sitemap Endpoint**: Generate XML sitemaps with a customizable URL path
- **Dynamic Content Discovery**: Automatically discover and include content items based on your configuration
- **Cache Optimization**: Built-in caching using Kentico's cache dependency system
- **Flexible Configuration**: Configure which content types, fields, and languages to include

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
    options.ContentTypeDependencies = new[] 
    { 
        "BlogPost", // or BlogPost.CONTENT_TYPE_NAME
        "Article", 
        "LandingPage" 
    };
});

var app = builder.Build();

// Your middleware configuration...
app.MapControllers();

app.Run();
```

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
        return _websiteDiscoveryProvider.GenerateRobotsTxt();
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
        => _provider.GenerateRobotsTxt();
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
    <loc>https://yoursite.com/about</loc>
    <lastmod>2025-10-03</lastmod>
    <changefreq>weekly</changefreq>
  </url>
  <url>
    <loc>https://yoursite.com/blog/article</loc>
    <lastmod>2025-10-03</lastmod>
    <changefreq>weekly</changefreq>
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
                node.Priority = 0.8;
            }
            else if (node.Url.Contains("/news/"))
            {
                node.ChangeFrequency = ChangeFrequency.Hourly;
                node.Priority = 0.9;
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
            Url = page.SystemFields.WebPageUrlPath,
            Title = page.Title,
            Description = page.Description,
            LastModified = DateTime.Now
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
