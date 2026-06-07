namespace Redhead.SitesCatalog.Application.Models.Analytics;

public abstract class AnalyticsQuery
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string? ClientId { get; init; }
    public string? Destination { get; init; }
    public string? Status { get; init; }
}
