using ActionResult = Microsoft.AspNetCore.Mvc.ActionResult;

namespace XperienceCommunity.SEO.Services;

public class WebsiteDiscoveryProvider(
    IProgressiveCache cache,
    IWebsiteChannelContext website,
    IContentQueryExecutor executor,
    IWebsiteDiscoveryOptions options,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IWebsiteDiscoveryProvider
{
    private readonly IProgressiveCache cache = cache;
    private readonly IWebsiteChannelContext website = website;
    private readonly IContentQueryExecutor executor = executor;
    private readonly IWebsiteDiscoveryOptions options = ValidateOptions(options);
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;
    private readonly IConfiguration configuration = configuration;

    /// <summary>
    /// Generates the sitemap.xml output dynamically.
    /// </summary>
    /// <returns></returns>
    public async Task<ActionResult> GenerateSitemap()
    {
        var sitemapItems = await GetSitemapPages();
        return new SitemapProvider().CreateSitemap(new SitemapModel(sitemapItems));
    }

    /// <summary>
    /// Generates the llms.txt file dynamically.
    /// </summary>
    /// <returns></returns>
    public async Task<ActionResult> GenerateLlmsTxt()
    {
        var pages = await GetSitemapPagesWithDetails();
        var sb = new StringBuilder();
        var currentRequest = httpContextAccessor.HttpContext?.Request;

        sb.AppendLine($"# {website.WebsiteChannelName}");
        sb.AppendLine();
        sb.AppendLine("## Pages");
        sb.AppendLine();

        foreach (var page in pages)
        {
            string title = !string.IsNullOrWhiteSpace(page.Title) ? page.Title : page.SystemFields.WebPageItemName;
            string relativeUrl = page.SystemFields.WebPageUrlPath;
            string url = currentRequest != null ? relativeUrl.AbsoluteURL(currentRequest) : relativeUrl;

            if (!string.IsNullOrWhiteSpace(page.Description))
            {
                sb.AppendLine($"- [{title.CleanHtml().EscapeMarkdown().Replace("â€™s", "")}]({url.ToLower()}): {page.Description.CleanHtml().EscapeMarkdown().Replace("â€™s", "")}");
            }
            else
            {
                sb.AppendLine($"- [{title.CleanHtml().EscapeMarkdown().Replace("â€™s", "")}]({url.ToLower()})");
            }
        }

        return new ContentResult
        {
            Content = sb.ToString(),
            ContentType = "text/plain; charset=utf-8"
        };
    }

    /// <summary>
    /// Generates the robots.txt file dynamically based on configuration.
    /// Reads content from appsettings.json key: XperienceCommunitySEO:RobotsContent
    /// </summary>
    /// <returns>ContentResult with robots.txt content</returns>
    public string GenerateRobotsTxt()
    {
        string robotsContent = configuration.GetValue<string>("XperienceCommunitySEO:RobotsContent")
            ?? string.Empty;

        // Clean up any extra whitespace/indentation and normalize line endings
        robotsContent = string.Join(Environment.NewLine,
            robotsContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.TrimStart()));

        return robotsContent;
    }

    /// <summary>
    /// Gets the sitemap pages with additional details like title and description.
    /// </summary>
    /// <returns></returns>
    public async Task<List<SitemapPage>> GetSitemapPagesWithDetails() =>
        await cache.LoadAsync(cs =>
        {
            cs.CacheDependency = CacheHelper.GetCacheDependency(BuildCacheDependencyKeys());

            return GetSitemapPagesInternal();
        }, new CacheSettings(60, [nameof(GetSitemapPagesWithDetails)]) { });

    /// <summary>
    /// Gets the sitemap pages for the sitemap.xml.
    /// </summary>
    /// <returns></returns>
    public async Task<List<SitemapNode>> GetSitemapPages() =>
        await cache.LoadAsync(cs =>
        {
            cs.CacheDependency = CacheHelper.GetCacheDependency(BuildCacheDependencyKeys());

            return GetSitemapNodesInternal();
        }, new CacheSettings(60, [nameof(GetSitemapPages)]) { });

    private static IWebsiteDiscoveryOptions ValidateOptions(IWebsiteDiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ReusableSchemaName))
        {
            throw new ArgumentException("ReusableSchemaName cannot be null or empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.DefaultLanguage))
        {
            throw new ArgumentException("DefaultLanguage cannot be null or empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.DescriptionFieldName))
        {
            throw new ArgumentException("DescriptionFieldName cannot be null or empty.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.TitleFieldName))
        {
            throw new ArgumentException("TitleFieldName cannot be null or empty.", nameof(options));
        }

        if (options.ContentTypeDependencies == null || options.ContentTypeDependencies.Length == 0)
        {
            throw new ArgumentException("ContentTypeDependencies must contain at least one content type.", nameof(options));
        }

        return options;
    }

    private async Task<List<SitemapNode>> GetSitemapNodesInternal()
    {
        var pages = await GetSitemapPagesInternal();
        var nodes = new List<SitemapNode>();

        foreach (var page in pages)
        {
            var node = new SitemapNode(page.SystemFields.WebPageUrlPath)
            {
                LastModificationDate = DateTime.Now,
                ChangeFrequency = ChangeFrequency.Weekly,
            };

            nodes.Add(node);
        }

        return nodes;
    }

    private async Task<List<SitemapPage>> GetSitemapPagesInternal()
    {
        var b = new ContentItemQueryBuilder()
            .ForContentTypes(c => c
                .OfContentType(options.ContentTypeDependencies)
                .ForWebsite(website.WebsiteChannelName))
            .InLanguage(options.DefaultLanguage);

        var pages = await executor.GetWebPageResult(b, c =>
        {
            bool isInSitemap = string.IsNullOrWhiteSpace(options.SitemapShowFieldName)
                ? true
                : c.TryGetValue(options.SitemapShowFieldName, out bool? val) && (val ?? false);

            string description = string.Empty;
            c.TryGetValue(options.DescriptionFieldName, out description);

            string title = string.Empty;
            c.TryGetValue(options.TitleFieldName, out title);

            return new SitemapPage(new()
            {
                WebPageItemID = c.WebPageItemID,
                WebPageItemGUID = c.WebPageItemGUID,
                WebPageItemName = c.WebPageItemName,
                WebPageItemOrder = c.WebPageItemOrder,
                WebPageItemTreePath = c.WebPageItemTreePath,
                WebPageUrlPath = c.WebPageUrlPath.ToLower(),

                ContentItemCommonDataContentLanguageID = c.ContentItemCommonDataContentLanguageID,
                ContentItemCommonDataVersionStatus = c.ContentItemCommonDataVersionStatus,
                ContentItemContentTypeID = c.ContentItemContentTypeID,
                ContentItemGUID = c.ContentItemGUID,
                ContentItemID = c.ContentItemID,
                ContentItemIsSecured = c.ContentItemIsSecured,
                ContentItemName = c.ContentItemName,
            }, isInSitemap, title, description);
        });

        return pages.Where(p => p.IsInSitemap).ToList();
    }

    private string[] BuildCacheDependencyKeys() =>
        options.ContentTypeDependencies
            .Select(t => $"webpageitem|bychannel|{website.WebsiteChannelName}|bycontenttype|{nameof(WebsiteDiscoveryProvider)}|{t}")
            .ToArray();
}
