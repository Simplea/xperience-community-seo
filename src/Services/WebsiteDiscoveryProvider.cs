using System.Globalization;
using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using ActionResult = Microsoft.AspNetCore.Mvc.ActionResult;

namespace XperienceCommunity.SEO.Services;

public class WebsiteDiscoveryProvider(
    IProgressiveCache cache,
    IWebsiteChannelContext website,
    IContentQueryExecutor executor,
    IWebPageUrlRetriever urlRetriever,
    IInfoProvider<ContentItemLanguageMetadataInfo> languageMetadataProvider,
    IWebsiteDiscoveryOptions options,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IWebsiteDiscoveryProvider
{
    private readonly IProgressiveCache cache = cache;
    private readonly IWebsiteChannelContext website = website;
    private readonly IContentQueryExecutor executor = executor;
    private readonly IWebPageUrlRetriever urlRetriever = urlRetriever;
    private readonly IInfoProvider<ContentItemLanguageMetadataInfo> languageMetadataProvider = languageMetadataProvider;
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

        sb.AppendLine($"# {GetLlmsTxtTitle()}");
        sb.AppendLine();
        sb.AppendLine("## Pages");
        sb.AppendLine();

        foreach (var page in pages)
        {
            string title = !string.IsNullOrWhiteSpace(page.Title) ? page.Title : page.DisplayName;
            string url = currentRequest != null ? page.Url.AbsoluteURL(currentRequest) : page.Url;

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
    /// Gets the sitemap pages of the current channel with additional details like title and description.
    /// </summary>
    /// <returns></returns>
    public async Task<List<SitemapPage>> GetSitemapPagesWithDetails() =>
        await cache.LoadAsync(cs =>
        {
            cs.CacheDependency = CacheHelper.GetCacheDependency(BuildCacheDependencyKeys());

            return GetSitemapPagesInternal();
        }, new CacheSettings(60, nameof(GetSitemapPagesWithDetails), website.WebsiteChannelName, options.DefaultLanguage));

    /// <summary>
    /// Gets the sitemap pages for the sitemap.xml. The nodes are built on every call, because
    /// the sitemap provider rewrites their URLs to absolute ones using the current request's host.
    /// </summary>
    /// <returns></returns>
    public async Task<List<SitemapNode>> GetSitemapPages()
    {
        var pages = await GetSitemapPagesWithDetails();

        return pages.Select(ToSitemapNode).ToList();
    }

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

    private static SitemapNode ToSitemapNode(SitemapPage page) =>
        new(page.Url)
        {
            // Dates are stored in the server's time, and the sitemap format needs the offset
            LastModificationDate = page.LastModified is { } lastModified ? DateTime.SpecifyKind(lastModified, DateTimeKind.Local) : null,
            ChangeFrequency = ParseChangeFrequency(page.ChangeFrequency) ?? ChangeFrequency.Weekly,
            Priority = ParsePriority(page.Priority),
        };

    private static ChangeFrequency? ParseChangeFrequency(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ChangeFrequency frequency) ? frequency : null;

    private static decimal? ParsePriority(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal priority)
            && priority is >= 0 and <= 1
            ? priority
            : null;

    private string GetLlmsTxtTitle() =>
        options.LlmsTxtTitles.TryGetValue(website.WebsiteChannelName, out string? title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : website.WebsiteChannelName;

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

            string priority = string.Empty;
            if (!string.IsNullOrWhiteSpace(options.SitemapPriorityFieldName))
            {
                c.TryGetValue(options.SitemapPriorityFieldName, out priority);
            }

            string frequency = string.Empty;
            if (!string.IsNullOrWhiteSpace(options.SitemapFrequencyFieldName))
            {
                c.TryGetValue(options.SitemapFrequencyFieldName, out frequency);
            }

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
            }, isInSitemap, title, description)
            {
                LastModified = c.ContentItemCommonDataLastPublishedWhen,
                Priority = priority,
                ChangeFrequency = frequency,
            };
        });

        var sitemapPages = pages.Where(p => p.IsInSitemap).ToList();

        // The URL retriever returns the canonical URL: the root for the home page, the selected URL for the others
        var urls = await urlRetriever.Retrieve(
            sitemapPages.Select(p => p.SystemFields.WebPageItemGUID).ToList(),
            website.WebsiteChannelName,
            options.DefaultLanguage,
            false);

        var displayNames = await GetDisplayNames(sitemapPages);

        return sitemapPages
            .Select(p =>
            {
                p.DisplayName = displayNames.TryGetValue((p.SystemFields.ContentItemID, p.SystemFields.ContentItemCommonDataContentLanguageID), out string? displayName)
                    ? displayName
                    : p.SystemFields.WebPageItemName;
                p.Url = urls.TryGetValue(p.SystemFields.WebPageItemGUID, out var url)
                    ? url.RelativePathTrimmed().ToLowerInvariant()
                    : $"/{p.SystemFields.WebPageUrlPath}";
                return p;
            })
            .ToList();
    }

    private async Task<Dictionary<(int ContentItemId, int LanguageId), string>> GetDisplayNames(List<SitemapPage> pages)
    {
        var metadata = await languageMetadataProvider.Get()
            .WhereIn(nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataContentItemID), pages.Select(p => p.SystemFields.ContentItemID).ToList())
            .WhereIn(nameof(ContentItemLanguageMetadataInfo.ContentItemLanguageMetadataContentLanguageID), pages.Select(p => p.SystemFields.ContentItemCommonDataContentLanguageID).Distinct().ToList())
            .GetEnumerableTypedResultAsync();

        return metadata.ToDictionary(
            m => (m.ContentItemLanguageMetadataContentItemID, m.ContentItemLanguageMetadataContentLanguageID),
            m => m.ContentItemLanguageMetadataDisplayName);
    }

    private string[] BuildCacheDependencyKeys() =>
        options.ContentTypeDependencies
            .Select(t => $"webpageitem|bychannel|{website.WebsiteChannelName}|bycontenttype|{t}")
            .ToArray();
}
