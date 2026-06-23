using Microsoft.AspNetCore.Identity;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public string? DisplayName { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public string? InvitationTokenHash { get; set; }
    public DateTime? InvitationExpiresAtUtc { get; set; }
    public string? SuperAdminNote { get; set; }

    public bool HasCompleteProfile => !string.IsNullOrWhiteSpace(DisplayName);

    public string EffectiveDisplayName =>
        HasCompleteProfile ? DisplayName!.Trim() : Email ?? UserName ?? string.Empty;

    /// <summary>
    /// Per-user export mode override. Null means inherit from role policy.
    /// </summary>
    public ExportLimitMode? ExportLimitOverrideMode { get; set; }

    /// <summary>
    /// Per-user row cap override. Non-null only when ExportLimitOverrideMode is Limited.
    /// </summary>
    public int? ExportLimitRowsOverride { get; set; }

    public int? DailyUniqueExportedDomainsLimitOverride { get; set; }

    public int? WeeklyUniqueExportedDomainsLimitOverride { get; set; }

    public int? DailyExportOperationsLimitOverride { get; set; }

    public int? WeeklyExportOperationsLimitOverride { get; set; }
}
