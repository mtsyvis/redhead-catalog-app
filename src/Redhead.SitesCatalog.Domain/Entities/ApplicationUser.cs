using Microsoft.AspNetCore.Identity;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public bool HasCompleteProfile =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName);

    public string DisplayName =>
        HasCompleteProfile
            ? $"{FirstName!.Trim()} {LastName!.Trim()}"
            : Email ?? UserName ?? string.Empty;

    /// <summary>
    /// Per-user export mode override. Null means inherit from role policy.
    /// </summary>
    public ExportLimitMode? ExportLimitOverrideMode { get; set; }

    /// <summary>
    /// Per-user row cap override. Non-null only when ExportLimitOverrideMode is Limited.
    /// </summary>
    public int? ExportLimitRowsOverride { get; set; }
}
