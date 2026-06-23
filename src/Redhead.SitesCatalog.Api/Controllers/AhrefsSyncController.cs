using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;
using Redhead.SitesCatalog.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/ahrefs-sync")]
[Authorize(Policy = AppPolicies.SuperAdminOnly)]
public sealed class AhrefsSyncController : ControllerBase
{
    private readonly IAhrefsSyncService _syncService;
    private readonly AhrefsSyncOptions _options;

    public AhrefsSyncController(
        IAhrefsSyncService syncService,
        IOptions<AhrefsSyncOptions> options)
    {
        _syncService = syncService;
        _options = options.Value;
    }

    [HttpPost("dry-run")]
    public async Task<ActionResult> DryRun(
        [FromBody] AhrefsSyncDryRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.MaxSitesOverride <= 0)
        {
            return BadRequest(new MessageResponse("MaxSitesOverride must be greater than zero."));
        }

        var result = await _syncService.DryRunAsync(
            request?.MaxSitesOverride,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("run")]
    public async Task<ActionResult> Run(
        [FromBody] AhrefsSyncRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaxSitesOverride <= 0)
        {
            return BadRequest(new MessageResponse("MaxSitesOverride must be greater than zero."));
        }

        var limited = request.MaxSitesOverride.HasValue;
        var result = await _syncService.RunAsync(
            new Application.Ahrefs.AhrefsSyncRequest(
                limited ? AhrefsSyncRunKind.ManualLimited : AhrefsSyncRunKind.ManualFull,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                request.MaxSitesOverride,
                request.SaveSnapshots ?? !limited,
                request.Force),
            cancellationToken);
        if (result.Conflict)
        {
            return Conflict(new MessageResponse(result.ConflictMessage!));
        }

        return Ok(result.Run);
    }

    [HttpGet("runs")]
    public async Task<ActionResult> ListRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            return BadRequest(new MessageResponse("Page must be greater than zero."));
        }

        if (pageSize is <= 0 or > 100)
        {
            return BadRequest(new MessageResponse("PageSize must be between 1 and 100."));
        }

        return Ok(await _syncService.ListRunsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var data = await _syncService.GetMonitoringDataAsync(refresh, cancellationToken);
        AhrefsSyncCronSchedule.TryParse(_options.Cron, out var schedule);
        var notBeforeUtc = _options.NotBeforeUtc?.UtcDateTime;
        var scheduleIsActive = !notBeforeUtc.HasValue ||
            data.LimitsCheckedAt >= notBeforeUtc.Value;
        var dueOccurrence = schedule?.GetDueOccurrenceUtc(data.LimitsCheckedAt);
        var nextScheduledRun = schedule?.GetNextOccurrenceUtc(data.LimitsCheckedAt);
        if (notBeforeUtc.HasValue &&
            nextScheduledRun.HasValue &&
            nextScheduledRun.Value < notBeforeUtc.Value)
        {
            nextScheduledRun = schedule?.GetNextOccurrenceUtc(
                notBeforeUtc.Value.AddTicks(-1));
        }
        var isWaitingForUsageReset =
            _options.Enabled &&
            scheduleIsActive &&
            dueOccurrence.HasValue &&
            data.ActiveRun == null &&
            !data.HasCompletedMonthlyRunForSnapshotMonth &&
            data.IsWaitingForUsageReset;
        var isDueNow =
            _options.Enabled &&
            scheduleIsActive &&
            dueOccurrence.HasValue &&
            data.ActiveRun == null &&
            !data.HasCompletedMonthlyRunForSnapshotMonth &&
            !isWaitingForUsageReset;
        var spendableUnits = Math.Max(0, data.EffectiveAvailableUnits - data.SafetyBufferUnits);
        var affordableSitesCount = AhrefsSyncCostCalculator.GetAffordableSiteCount(
            spendableUnits,
            data.EligibleSitesCount,
            data.BatchSize);
        var plannedSitesCount = Math.Min(
            data.EligibleSitesCount,
            Math.Min(affordableSitesCount, data.MaxSitesPerRun));
        var fullCatalogFitsBudget = affordableSitesCount >= data.EligibleSitesCount;
        var configuredRunLimitedByBudget =
            plannedSitesCount < Math.Min(data.EligibleSitesCount, data.MaxSitesPerRun);
        var configuredRunLimitedByMaxSites =
            plannedSitesCount < Math.Min(data.EligibleSitesCount, affordableSitesCount);

        return Ok(new AhrefsSyncStatusResponse(
            _options.Enabled,
            _options.Cron,
            notBeforeUtc,
            nextScheduledRun,
            isDueNow,
            isWaitingForUsageReset,
            dueOccurrence,
            data.LimitsCheckedAt,
            data.Limits.UsageResetDate,
            data.ApiKeyRemainingUnits,
            data.WorkspaceRemainingUnits,
            data.AppBudgetRemainingUnits,
            data.EffectiveAvailableUnits,
            data.SafetyBufferUnits,
            spendableUnits,
            data.EligibleSitesCount,
            data.FullEstimatedUnits,
            affordableSitesCount,
            plannedSitesCount,
            AhrefsSyncCostCalculator.EstimateUnits(plannedSitesCount, data.BatchSize),
            plannedSitesCount > 0 &&
                data.ActiveRun == null &&
                scheduleIsActive &&
                !isWaitingForUsageReset &&
                !data.HasCompletedMonthlyRunForSnapshotMonth,
            fullCatalogFitsBudget,
            Math.Max(0, data.FullEstimatedUnits - spendableUnits),
            configuredRunLimitedByBudget,
            configuredRunLimitedByMaxSites,
            data.BatchSize,
            data.MaxSitesPerRun,
            data.TargetMode,
            data.Protocol,
            data.VolumeMode,
            data.HasCompletedMonthlyRunForSnapshotMonth,
            data.ActiveRun));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult> GetRun(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            return BadRequest(new MessageResponse("Page must be greater than zero."));
        }

        if (pageSize is <= 0 or > 500)
        {
            return BadRequest(new MessageResponse("PageSize must be between 1 and 500."));
        }

        var result = await _syncService.GetRunAsync(id, page, pageSize, cancellationToken);
        return result == null
            ? NotFound(new MessageResponse("Ahrefs sync run not found."))
            : Ok(result);
    }
}
