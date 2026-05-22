namespace Redhead.SitesCatalog.Domain.Entities;

public class UserTablePreference
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TableKey { get; set; } = string.Empty;
    public string ActiveViewType { get; set; } = string.Empty;
    public string ActiveViewKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
