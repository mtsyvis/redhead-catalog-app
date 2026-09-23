using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/application-settings")]
[Authorize(Policy = AppPolicies.RoleSettingsReadAccess)]
public sealed class ApplicationSettingsController(IOptions<ClientCatalogOptions> clientCatalogOptions)
    : ControllerBase
{
    [HttpGet("limits")]
    public ActionResult<ApplicationSettingsLimitsResponse> GetLimits()
    {
        var clientCatalog = clientCatalogOptions.Value;
        return Ok(new ApplicationSettingsLimitsResponse(
            ClientCatalogLimits.DefaultSelectionLimit,
            ClientCatalogLimits.MaxSelectionLimit,
            clientCatalog.RequestsPerMinute,
            clientCatalog.UniqueSitesPerFiveMinutes,
            clientCatalog.AlertUniqueSitesPerHour,
            MultiSearchConstants.MaxInputs,
            StopListConstants.MaxStopListDomains,
            LiteMultiSearchConstants.MaxDomainsPerRequest,
            LiteMultiSearchConstants.MonthlyDomainLimit,
            TableViewConstants.CustomViewsPerUserTableLimit,
            SavedFilterSetConstants.FilterSetsPerUserTableLimit));
    }
}
