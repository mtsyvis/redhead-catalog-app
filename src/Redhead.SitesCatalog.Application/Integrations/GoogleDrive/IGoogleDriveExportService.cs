using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

public interface IGoogleDriveExportService
{
    Task<GoogleDriveExportResponse> ExportSitesAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken);

    Task<GoogleDriveExportResponse> ExportMultiSearchAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken);
}
