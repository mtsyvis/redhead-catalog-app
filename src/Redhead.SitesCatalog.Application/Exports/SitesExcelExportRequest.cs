using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed record SitesExcelExportRequest(
    IReadOnlyList<Site> Sites,
    IReadOnlyList<string> NotFoundDomains,
    IReadOnlyList<string> VisibleColumnKeys,
    string ExportColumnRole,
    string GeneratedBy,
    string RoleLabel,
    int RequestedRows,
    int ExportedRows,
    bool Truncated,
    int? LimitRows,
    bool NotFoundIncluded);
