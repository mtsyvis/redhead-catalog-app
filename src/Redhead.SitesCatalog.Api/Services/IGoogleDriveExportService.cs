using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Api.Services;

public interface IGoogleDriveExportService
{
    Task<GoogleDriveExportResponse> ExportSitesAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken);

    Task<GoogleDriveExportResponse> ExportMultiSearchAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken);
}
