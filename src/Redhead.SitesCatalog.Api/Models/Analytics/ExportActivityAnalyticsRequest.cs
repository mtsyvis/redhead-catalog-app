namespace Redhead.SitesCatalog.Api.Models.Analytics;

public sealed class ExportActivityAnalyticsRequest
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? ClientId { get; set; }
    public string? Destination { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
