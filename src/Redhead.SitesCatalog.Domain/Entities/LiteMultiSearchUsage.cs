namespace Redhead.SitesCatalog.Domain.Entities;

public class LiteMultiSearchUsage
{
    public string UserId { get; set; } = string.Empty;

    public DateTime MonthStartUtc { get; set; }

    public int DomainsUsed { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
