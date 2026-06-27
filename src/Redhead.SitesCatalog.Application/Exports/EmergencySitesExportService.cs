using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed class EmergencySitesExportService : IEmergencySitesExportService
{
    private const string GeneratedBy = "system";
    private const string RoleLabel = "System";
    private const string DefaultFilePrefix = "redhead-sites-full";

    private readonly ApplicationDbContext _context;
    private readonly ISitesExcelExportGenerator _excelExportGenerator;

    public EmergencySitesExportService(
        ApplicationDbContext context,
        ISitesExcelExportGenerator excelExportGenerator)
    {
        _context = context;
        _excelExportGenerator = excelExportGenerator;
    }

    public Task<EmergencySitesExportResult> GenerateAsync(CancellationToken cancellationToken = default)
        => GenerateAsync(DefaultFilePrefix, cancellationToken);

    public async Task<EmergencySitesExportResult> GenerateAsync(
        string filePrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);

        var sites = await _context.Sites
            .AsNoTracking()
            .Include(site => site.CanonicalLocation)
            .OrderBy(site => site.Domain)
            .ToListAsync(cancellationToken);

        await AttachPricingAsync(sites, cancellationToken);

        var stream = _excelExportGenerator.Generate(new SitesExcelExportRequest(
            sites,
            [],
            SitesExportColumnRegistry.GetDefaultColumnKeysForRole(AppRoles.SuperAdmin),
            null,
            AppRoles.SuperAdmin,
            GeneratedBy,
            RoleLabel,
            sites.Count,
            sites.Count,
            Truncated: false,
            LimitRows: null,
            NotFoundIncluded: false,
            PriceCellMode: SitesExcelPriceCellMode.AllTerms));

        return new EmergencySitesExportResult(
            CreateFileName(filePrefix, DateTime.UtcNow),
            ExportConstants.ExcelContentType,
            stream,
            sites.Count,
            stream.Length);
    }

    internal static string CreateFileName(DateTime utcNow)
        => CreateFileName(DefaultFilePrefix, utcNow);

    internal static string CreateFileName(string filePrefix, DateTime utcNow)
        => $"{filePrefix.Trim()}-{utcNow:yyyy-MM-dd}.xlsx";

    private async Task AttachPricingAsync(
        IReadOnlyList<Site> sites,
        CancellationToken cancellationToken)
    {
        if (sites.Count == 0)
        {
            return;
        }

        var domains = sites
            .Select(site => site.Domain)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var priceOptions = await _context.SitePriceOptions
            .AsNoTracking()
            .Where(priceOption => domains.Contains(priceOption.SiteDomain))
            .ToListAsync(cancellationToken);

        var serviceAvailabilities = await _context.SiteServiceAvailabilities
            .AsNoTracking()
            .Where(availability => domains.Contains(availability.SiteDomain))
            .ToListAsync(cancellationToken);

        var priceOptionsByDomain = priceOptions.ToLookup(
            priceOption => priceOption.SiteDomain,
            StringComparer.Ordinal);
        var serviceAvailabilitiesByDomain = serviceAvailabilities.ToLookup(
            availability => availability.SiteDomain,
            StringComparer.Ordinal);

        foreach (var site in sites)
        {
            site.PriceOptions = priceOptionsByDomain[site.Domain].ToList();
            site.ServiceAvailabilities = serviceAvailabilitiesByDomain[site.Domain].ToList();
        }
    }
}
