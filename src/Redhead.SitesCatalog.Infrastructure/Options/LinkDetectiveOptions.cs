namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class LinkDetectiveOptions
{
    public const string SectionName = "LinkDetective";
    public string? ApiToken { get; set; }
    public int CacheLifetimeDays { get; set; } = 30;

    public static bool IsValid(LinkDetectiveOptions options) => options.CacheLifetimeDays > 0;
}
