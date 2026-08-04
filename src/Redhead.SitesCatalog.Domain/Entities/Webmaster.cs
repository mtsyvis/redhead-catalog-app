namespace Redhead.SitesCatalog.Domain.Entities;

public class Webmaster
{
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string ContactRawText { get; set; } = string.Empty;
    public string NormalizedContactRawText { get; set; } = string.Empty;
    public string? PrimaryEmail { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<SiteWebmasterOffer> Offers { get; set; } = [];
}
