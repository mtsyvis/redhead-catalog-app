using Microsoft.AspNetCore.Identity;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Per-user export mode override. Null means inherit from role policy.
    /// </summary>
    public ExportLimitMode? ExportLimitOverrideMode { get; set; }

    /// <summary>
    /// Per-user row cap override. Non-null only when ExportLimitOverrideMode is Limited.
    /// </summary>
    public int? ExportLimitRowsOverride { get; set; }
}
