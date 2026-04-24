using HtmlAgilityPack;
using ReverseMarkdown;
using XperienceCommunity.SEO.Models;

namespace XperienceCommunity.SEO.Middleware;

public class MarkdownNegotiationMiddleware(RequestDelegate next, MarkdownNegotiationOptions options)
{
    private static readonly Converter converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Drop,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    public async Task InvokeAsync(HttpContext context)
    {
        var acceptHeader = context.Request.Headers.Accept.ToString();

        if (!acceptHeader.Contains("text/markdown", StringComparison.OrdinalIgnoreCase)
            || IsPathSkipped(context.Request.Path))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var capture = new MarkdownBodyCapture(originalBody);
        context.Response.Body = capture;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var contentType = context.Response.ContentType ?? string.Empty;
        if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var capturedBytes = Encoding.UTF8.GetBytes(capture.GetCapturedText());
            await originalBody.WriteAsync(capturedBytes);
            return;
        }

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        var html = PreprocessHtml(capture.GetCapturedText(), baseUrl);
        var markdown = converter.Convert(html);
        var markdownBytes = Encoding.UTF8.GetBytes(markdown);

        context.Response.ContentType = "text/markdown; charset=utf-8";
        context.Response.Headers["x-markdown-tokens"] = EstimateTokens(markdown).ToString();
        context.Response.ContentLength = markdownBytes.Length;

        await originalBody.WriteAsync(markdownBytes);
    }

    private bool IsPathSkipped(PathString requestPath)
    {
        var path = requestPath.Value ?? string.Empty;

        foreach (var entry in options.SkippedPaths)
        {
            if (entry.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = entry[..^2];
                if (path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                    || path.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (path.Equals(entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string PreprocessHtml(string html, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Strip by tag name
        foreach (var tag in options.StripElements)
        {
            foreach (var node in doc.DocumentNode.SelectNodes($"//{tag}") ?? Enumerable.Empty<HtmlNode>())
                node.Remove();
        }

        // Strip by CSS class
        foreach (var cls in options.StripClasses)
        {
            var xpath = $"//*[contains(concat(' ', normalize-space(@class), ' '), ' {cls} ')]";
            foreach (var node in doc.DocumentNode.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>())
                node.Remove();
        }

        // Unwrap <figure> — replace with inner content to avoid ReverseMarkdown wrapping
        // the whole card in an extra blockquote layer
        foreach (var figure in doc.DocumentNode.SelectNodes("//figure") ?? Enumerable.Empty<HtmlNode>())
        {
            var parent = figure.ParentNode;
            foreach (var child in figure.ChildNodes.ToList())
                parent.InsertBefore(child, figure);
            figure.Remove();
        }

        // Remove wrapper <p class="mb-0"> that contains another <p> — nested <p> is invalid
        // HTML; HAP splits it into two blocks producing blank blockquote lines
        foreach (var p in doc.DocumentNode.SelectNodes("//p[p]") ?? Enumerable.Empty<HtmlNode>())
        {
            var parent = p.ParentNode;
            foreach (var child in p.ChildNodes.ToList())
                parent.InsertBefore(child, p);
            p.Remove();
        }

        // Rewrite relative src/href — strip leading ~/ and prepend scheme+host
        foreach (var node in doc.DocumentNode.SelectNodes("//*[@src or @href]") ?? Enumerable.Empty<HtmlNode>())
        {
            RewriteUrl(node, "src", baseUrl);
            RewriteUrl(node, "href", baseUrl);
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static void RewriteUrl(HtmlNode node, string attribute, string baseUrl)
    {
        var value = node.GetAttributeValue(attribute, string.Empty);
        if (string.IsNullOrEmpty(value)) return;

        // Strip tilde prefix used by ASP.NET app-relative paths
        if (value.StartsWith("~/", StringComparison.Ordinal))
            value = value[1..];

        // Prepend baseUrl for root-relative paths (skip absolute URLs and anchors)
        if (value.StartsWith("/", StringComparison.Ordinal))
            node.SetAttributeValue(attribute, baseUrl + value);
    }

    private static int EstimateTokens(string text) =>
        text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
}
