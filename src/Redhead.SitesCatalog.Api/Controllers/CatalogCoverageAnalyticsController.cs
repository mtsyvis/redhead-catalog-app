using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services.Analytics.CatalogCoverageAnalytics;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/analytics/catalog-coverage")]
[Authorize(Policy = AppPolicies.AnalyticsReadAccess)]
public sealed class CatalogCoverageAnalyticsController(ICatalogCoverageAnalyticsService service) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CatalogCoverageAnalyticsDto>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetAsync(cancellationToken));

    [HttpGet("domains-without-offers.xlsx")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<FileStreamResult> ExportDomainsWithoutOffers(CancellationToken cancellationToken)
        => File(await service.ExportDomainsWithoutOffersAsync(cancellationToken),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"catalog-domains-without-offers-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
}
