using Microsoft.AspNetCore.Builder;
using XperienceCommunity.SEO.Middleware;
using XperienceCommunity.SEO.Models;

namespace XperienceCommunity.SEO;

/// <summary>
/// Extension methods for configuring sitemap services
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds middleware that serves a Markdown rendition of HTML pages when the request
    /// includes <c>Accept: text/markdown</c>. Place this call early in the pipeline,
    /// before <c>UseRouting</c> / <c>MapControllers</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureOptions">
    /// Optional configuration for path filtering and element stripping.
    /// When omitted, all paths are eligible and default chrome elements
    /// (header, nav, footer, script, style, noscript) are stripped.
    /// </param>
    public static IApplicationBuilder UseMarkdownContentNegotiation(
        this IApplicationBuilder app,
        Action<MarkdownNegotiationOptions>? configureOptions = null)
    {
        var opts = new MarkdownNegotiationOptions();
        configureOptions?.Invoke(opts);
        return app.UseMiddleware<MarkdownNegotiationMiddleware>(opts);
    }

    /// <summary>
    /// Adds Website Discovery services with required configuration
    /// </summary>
    public static IServiceCollection AddWebsiteDiscoveryProvider(this IServiceCollection services, Action<WebsiteDiscoveryOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new WebsiteDiscoveryOptions();
        configureOptions(options);

        // Validate required options
        if (string.IsNullOrWhiteSpace(options.ReusableSchemaName))
        {
            throw new InvalidOperationException("WebsiteDiscoveryOptions.ReusableSchemaName must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultLanguage))
        {
            throw new InvalidOperationException("WebsiteDiscoveryOptions.DefaultLanguage must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.DescriptionFieldName))
        {
            throw new InvalidOperationException("WebsiteDiscoveryOptions.DescriptionFieldName must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.TitleFieldName))
        {
            throw new InvalidOperationException("WebsiteDiscoveryOptions.TitleFieldName must be configured.");
        }

        if (options.ContentTypeDependencies == null || options.ContentTypeDependencies.Length == 0)
        {
            throw new InvalidOperationException("SitemapOptions.ContentTypeDependencies must contain at least one content type.");
        }

        services.AddSingleton<IWebsiteDiscoveryOptions>(options);
        services.AddScoped<IWebsiteDiscoveryProvider, WebsiteDiscoveryProvider>();
        return services;
    }
}
