namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string? BaseUrl { get; set; }

    public static bool IsValid(FrontendOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        return string.IsNullOrEmpty(baseUri.UserInfo) &&
            string.IsNullOrEmpty(baseUri.Query) &&
            string.IsNullOrEmpty(baseUri.Fragment) &&
            (string.IsNullOrEmpty(baseUri.AbsolutePath) || baseUri.AbsolutePath == "/");
    }
}
