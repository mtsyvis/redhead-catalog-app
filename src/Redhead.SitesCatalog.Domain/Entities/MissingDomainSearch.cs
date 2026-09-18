namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class MissingDomainSearch
{
    public Guid SearchId { get; set; }
    public MultiSearchAnalyticsRequest Search { get; set; } = null!;
    public string Domain { get; set; } = string.Empty;
}
