namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class AhrefsOptions
{
    public const string SectionName = "Ahrefs";
    public const string DefaultBaseUrl = "https://api.ahrefs.com/v3";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public static bool IsValid(AhrefsOptions options)
        => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl) &&
            baseUrl.Scheme == Uri.UriSchemeHttps;
}
