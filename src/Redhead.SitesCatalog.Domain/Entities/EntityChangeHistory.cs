namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class EntityChangeHistory
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string ChangesJson { get; set; } = string.Empty;
}
