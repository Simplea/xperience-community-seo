namespace XperienceCommunity.SEO.Models;

public struct SitemapPage(WebPageFields systemFields, bool isInSitemap, string title, string description) : IWebPageFieldsSource
{
    public WebPageFields SystemFields { get; set; } = systemFields;
    public bool IsInSitemap { get; set; } = isInSitemap;
    public string Title { get; set; } = title;
    public string Description { get; set; } = description;

    /// <summary>
    /// The page's canonical URL, relative to the site root ("/" for the home page)
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The name editors give the page, used when it has no title
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When the page was last published
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// The value of the configured priority field, if any
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// The value of the configured change frequency field, if any
    /// </summary>
    public string ChangeFrequency { get; set; } = string.Empty;
}
