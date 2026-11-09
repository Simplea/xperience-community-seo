namespace XperienceCommunity.SEO.Models;

/// <summary>
/// Configuration options for WebsiteDiscovery generation
/// </summary>
public interface IWebsiteDiscoveryOptions
{
    /// <summary>
    /// The reusable schema name to filter content items
    /// </summary>
    public string ReusableSchemaName { get; }

    /// <summary>
    /// The default language for sitemap content
    /// </summary>
    public string DefaultLanguage { get; }

    /// <summary>
    /// Field name for sitemap visibility flag (optional - if empty, all pages are included)
    /// </summary>
    public string SitemapShowFieldName { get; }

    /// <summary>
    /// Field name for description
    /// </summary>
    public string DescriptionFieldName { get; }

    /// <summary>
    /// Field name for title
    /// </summary>
    public string TitleFieldName { get; }

    /// <summary>
    /// Content type names to include in sitemap
    /// </summary>
    public string[] ContentTypeDependencies { get; }

    /// <summary>
    /// Field name for the sitemap priority, a value from 0.0 to 1.0 (optional - if empty, no priority is listed)
    /// </summary>
    public string SitemapPriorityFieldName { get; }

    /// <summary>
    /// Field name for the sitemap change frequency (optional - if empty, or the page has no value, "weekly" is listed)
    /// </summary>
    public string SitemapFrequencyFieldName { get; }

    /// <summary>
    /// The llms.txt heading per website channel name (optional - a channel without an entry uses the channel name)
    /// </summary>
    public IDictionary<string, string> LlmsTxtTitles { get; }
}
