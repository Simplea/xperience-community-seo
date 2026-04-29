namespace XperienceCommunity.SEO.Models;

public class MarkdownNegotiationOptions
{
    /// <summary>
    /// Paths to skip for Markdown negotiation, even when <c>Accept: text/markdown</c> is present.
    /// A trailing <c>/*</c> suffix skips all paths under that prefix (e.g. <c>/api/*</c>).
    /// An entry without a wildcard is matched exactly (e.g. <c>/health</c>).
    /// When empty, no paths are skipped.
    /// </summary>
    public string[] SkippedPaths { get; set; } = [];

    /// <summary>
    /// HTML element tag names to strip before converting to Markdown (e.g. "nav", "header", "footer", "script", "style").
    /// Defaults to common chrome elements that add noise for agents.
    /// </summary>
    public string[] StripElements { get; set; } = ["header", "nav", "footer", "script", "style", "noscript"];

    /// <summary>
    /// CSS class names whose elements should be removed before converting to Markdown.
    /// Any element that contains one of these classes is removed entirely (e.g. "page-spinner").
    /// When empty, no class-based stripping is applied.
    /// </summary>
    public string[] StripClasses { get; set; } = [];
}
