using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of validating and mapping a single import row to a Site or error.
/// </summary>
public sealed record RowValidationResult(Site? Site, SitesImportError? Error)
{
    public bool IsSuccess => Error is null && Site is not null;

    public static RowValidationResult Fail(SitesImportError error) => new(null, error);

    public static RowValidationResult Ok(Site site) => new(site, null);

    /// <summary>
    /// Represents a row that should be ignored (e.g., empty trailing row).
    /// </summary>
    public static RowValidationResult Skip() => new(null, null);
}
