namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class LinkDetectivePriceCache
{
    public string SiteDomain { get; set; } = string.Empty;
    public decimal? MinimumUsd { get; set; }
    public decimal? AverageUsd { get; set; }
    public decimal? MedianUsd { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
    public string? LastError { get; set; }
}
