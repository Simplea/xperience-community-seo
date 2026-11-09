namespace XperienceCommunity.SEO.Extensions;

public static class UrlExtensions
{
    public static string RelativePathTrimmed(this WebPageUrl pageUrl) => pageUrl.RelativePath.TrimStart('~');

    public static string AbsoluteURL(this WebPageUrl pageUrl, HttpRequest currentRequest) =>
        $"{currentRequest.Scheme}://{currentRequest.Host}{currentRequest.PathBase}{pageUrl.RelativePathTrimmed()}";

    public static string AbsoluteURL(this string relativeUrl, HttpRequest currentRequest) =>
        $"{currentRequest.Scheme}://{currentRequest.Host}{currentRequest.PathBase}/{relativeUrl.TrimStart('~').TrimStart('/')}";
}
