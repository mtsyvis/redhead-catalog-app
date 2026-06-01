using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed class EmergencySitesExportService : IEmergencySitesExportService
{
    private const string GeneratedBy = "system";
    private const string RoleLabel = "System";

    private readonly ApplicationDbContext _context;
    private readonly ISitesExcelExportGenerator _excelExportGenerator;

    public EmergencySitesExportService(
        ApplicationDbContext context,
        ISitesExcelExportGenerator excelExportGenerator)
    {
        _context = context;
        _excelExportGenerator = excelExportGenerator;
    }

    public async Task<EmergencySitesExportResult> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var sites = await _context.Sites
            .AsNoTracking()
            .Include(site => site.CanonicalLocation)
            .OrderBy(site => site.Domain)
            .ToListAsync(cancellationToken);

        var stream = _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            [],
            SitesExportColumnRegistry.GetDefaultColumnKeysForRole(AppRoles.SuperAdmin),
            AppRoles.SuperAdmin,
            GeneratedBy,
            RoleLabel,
            sites.Count,
            sites.Count,
            Truncated: false,
            LimitRows: null,
            NotFoundIncluded: false));

        return new EmergencySitesExportResult(
            CreateFileName(DateTime.UtcNow),
            ExportConstants.ExcelContentType,
            stream,
            sites.Count,
            stream.Length);
    }

    internal static string CreateFileName(DateTime utcNow)
        => $"redhead-sites-full-{utcNow:yyyy-MM-dd}.xlsx";
}
