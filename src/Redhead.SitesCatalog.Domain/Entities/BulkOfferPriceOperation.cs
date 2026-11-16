namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class BulkOfferPriceOperation
{
    public Guid Id { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public string PrimaryEmail { get; set; } = string.Empty;
    public int UpdatedCount { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
}
