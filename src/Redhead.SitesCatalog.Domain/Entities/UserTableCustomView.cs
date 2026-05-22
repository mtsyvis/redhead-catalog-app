namespace Redhead.SitesCatalog.Domain.Entities;

public class UserTableCustomView
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TableKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string SettingsJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
