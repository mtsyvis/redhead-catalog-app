namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class LinkDetectiveOptions
{
    public const string SectionName = "LinkDetective";
    public const int MaxCacheLifetimeDays = 365;
    public string? ApiToken { get; set; }
    public int CacheLifetimeDays { get; set; } = 30;

    public static bool IsValid(LinkDetectiveOptions options) =>
        options.CacheLifetimeDays is >= 1 and <= MaxCacheLifetimeDays;

    public DateTime FreshnessCutoffUtc(DateTime now) => now.AddDays(-CacheLifetimeDays);
}
