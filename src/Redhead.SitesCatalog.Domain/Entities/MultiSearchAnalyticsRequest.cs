namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class MultiSearchAnalyticsRequest
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime SearchedAtUtc { get; set; }
    public List<MissingDomainSearch> MissingDomains { get; set; } = [];
}
