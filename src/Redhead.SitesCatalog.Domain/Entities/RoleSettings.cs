using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class RoleSettings
{
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Explicit export mode for this role. Must never be null.
    /// </summary>
    public ExportLimitMode ExportLimitMode { get; set; }

    /// <summary>
    /// Maximum rows to export. Non-null only when ExportLimitMode is Limited.
    /// </summary>
    public int? ExportLimitRows { get; set; }
}
