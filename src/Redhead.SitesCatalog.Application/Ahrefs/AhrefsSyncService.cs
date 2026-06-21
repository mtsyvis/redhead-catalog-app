using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Concurrency;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Ahrefs;

public sealed class AhrefsSyncService : IAhrefsSyncService
{
    public const string SnapshotSource = "AhrefsMonthlySync";

    private readonly ApplicationDbContext _context;
    private readonly IAhrefsApiClient _apiClient;
    private readonly IAhrefsSyncLock _syncLock;
    private readonly AhrefsSyncOptions _options;
    private readonly ILogger<AhrefsSyncService> _logger;

    public AhrefsSyncService(
        ApplicationDbContext context,
        IAhrefsApiClient apiClient,
        IAhrefsSyncLock syncLock,
        IOptions<AhrefsSyncOptions> options,
        ILogger<AhrefsSyncService> logger)
    {
        _context = context;
        _apiClient = apiClient;
        _syncLock = syncLock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AhrefsSyncDryRunResult> DryRunAsync(
        int? maxSitesOverride,
        CancellationToken cancellationToken)
    {
        await using var lockHandle = await _syncLock.TryAcquireAsync(cancellationToken);
        var eligibleCount = await EligibleSitesQuery().CountAsync(cancellationToken);
        var limits = await _apiClient.GetLimitsAndUsageAsync(cancellationToken);
        var availableUnits = GetEffectiveAvailableUnits(limits);
        var selection = CalculateSelection(
            eligibleCount,
            Math.Min(maxSitesOverride ?? _options.MaxSitesPerRun, _options.MaxSitesPerRun),
            availableUnits);
        var snapshotMonth = GetSnapshotMonth(DateTime.UtcNow);
        var alreadyCompleted = maxSitesOverride == null &&
            await HasSuccessfulFullRunAsync(snapshotMonth, cancellationToken);
        var reason = lockHandle == null
            ? "An Ahrefs sync is already running."
            : alreadyCompleted
                ? "A successful full coverage sync already exists for this snapshot month."
                : selection.SelectedSitesCount <= 0
                    ? "No sites are affordable after applying the configured safety buffer."
                    : null;
        var canRun = reason == null;

        return new AhrefsSyncDryRunResult(
            eligibleCount,
            selection.SelectedSitesCount,
            AhrefsSyncCostCalculator.CostPerSite,
            selection.FullEstimatedUnits,
            selection.SelectedEstimatedUnits,
            availableUnits,
            _options.SafetyBufferUnits,
            canRun,
            reason,
            _options.TargetMode,
            _options.Protocol,
            _options.VolumeMode,
            snapshotMonth,
            limits.UsageResetDate,
            selection.WasLimitedByBudget);
    }

    public async Task<AhrefsSyncRunResult> RunAsync(
        AhrefsSyncRequest request,
        CancellationToken cancellationToken)
    {
        await using var lockHandle = await _syncLock.TryAcquireAsync(cancellationToken);
        if (lockHandle == null)
        {
            return AhrefsSyncRunResult.AlreadyRunning();
        }

        var now = DateTime.UtcNow;
        var snapshotMonth = GetSnapshotMonth(now);
        if (!request.Force && IsFullRun(request.RunKind) &&
            await HasSuccessfulFullRunAsync(snapshotMonth, cancellationToken))
        {
            var skipped = CreateRun(request, now, snapshotMonth);
            skipped.Status = AhrefsSyncRunStatus.SkippedAlreadyCompleted;
            skipped.FinishedAt = DateTime.UtcNow;
            skipped.ErrorMessage = "A successful full coverage sync already exists for this snapshot month.";
            _context.AhrefsSyncRuns.Add(skipped);
            await _context.SaveChangesAsync(cancellationToken);
            return AhrefsSyncRunResult.Completed(skipped);
        }

        var run = CreateRun(request, now, snapshotMonth);
        _context.AhrefsSyncRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var limits = await _apiClient.GetLimitsAndUsageAsync(cancellationToken);
            run.UsageResetDate = limits.UsageResetDate;
            run.AvailableUnitsBefore = GetEffectiveAvailableUnits(limits);

            run.EligibleSitesCount = await EligibleSitesQuery().CountAsync(cancellationToken);
            var maxSites = Math.Min(
                request.MaxSitesOverride ?? _options.MaxSitesPerRun,
                _options.MaxSitesPerRun);
            var selection = CalculateSelection(
                run.EligibleSitesCount,
                maxSites,
                run.AvailableUnitsBefore);
            ApplySelection(run, selection);

            if (run.SelectedSitesCount <= 0)
            {
                run.Status = AhrefsSyncRunStatus.SkippedInsufficientUnits;
                run.ErrorMessage = "No sites are affordable after applying the configured safety buffer.";
                run.FinishedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return AhrefsSyncRunResult.Completed(run);
            }

            var sites = await EligibleSitesQuery()
                .Take(run.SelectedSitesCount)
                .Select(site => new SelectedSite(site.Domain, site.Traffic, site.DR))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            await ProcessSitesAsync(run, sites, request.SaveSnapshots, cancellationToken);

            run.IsFullCoverage =
                IsFullRun(request.RunKind) &&
                run.SelectedSitesCount == run.EligibleSitesCount &&
                run.UpdatedSitesCount == run.EligibleSitesCount &&
                run.FailedSitesCount == 0 &&
                run.SkippedSitesCount == 0;

            if (run.Status == AhrefsSyncRunStatus.Running)
            {
                if (run.UpdatedSitesCount == 0 && run.FailedSitesCount > 0)
                {
                    run.Status = AhrefsSyncRunStatus.Failed;
                    run.ErrorMessage = "All selected Ahrefs updates failed.";
                }
                else
                {
                    run.Status = run.IsFullCoverage
                        ? AhrefsSyncRunStatus.Succeeded
                        : AhrefsSyncRunStatus.SucceededPartial;
                }
            }

            run.AvailableUnitsAfter = Math.Max(0, run.AvailableUnitsBefore - run.ActualUnits);
            run.FinishedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return AhrefsSyncRunResult.Completed(run);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryFinalizeFailedRunAsync(
                run,
                AhrefsSyncRunStatus.Cancelled,
                "Ahrefs sync was cancelled.",
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ahrefs sync failed. RunId={RunId}", run.Id);
            var failedRun = await TryFinalizeFailedRunAsync(
                run,
                AhrefsSyncRunStatus.Failed,
                ex.Message,
                CancellationToken.None);
            return AhrefsSyncRunResult.Completed(failedRun ?? run);
        }
    }

    public async Task<IReadOnlyList<AhrefsSyncRun>> ListRunsAsync(
        int take,
        CancellationToken cancellationToken)
        => await _context.AhrefsSyncRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

    public async Task<AhrefsSyncRunDetails?> GetRunAsync(
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var run = await _context.AhrefsSyncRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (run == null)
        {
            return null;
        }

        var itemsQuery = _context.AhrefsSyncRunItems
            .AsNoTracking()
            .Where(item => item.RunId == id);
        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= totalCount
            ? []
            : await itemsQuery
                .OrderBy(item => item.Domain)
                .ThenBy(item => item.Id)
                .Skip((int)skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new AhrefsSyncRunDetails(
            run,
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    internal IQueryable<Site> EligibleSitesQuery()
        => _context.Sites
            .Where(site => !site.IsQuarantined)
            .OrderByDescending(site => site.Traffic)
            .ThenBy(site => site.Domain);

    private async Task ProcessSitesAsync(
        AhrefsSyncRun run,
        IReadOnlyList<SelectedSite> sites,
        bool saveSnapshots,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < sites.Count; offset += run.BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = sites.Skip(offset).Take(run.BatchSize).ToList();
            var domains = batch.Select(site => site.Domain).ToList();
            var trackedSites = await _context.Sites
                .Where(site => domains.Contains(site.Domain))
                .ToListAsync(cancellationToken);
            var sitesByDomain = trackedSites.ToDictionary(site => site.Domain);
            var availableSites = new List<Site>(batch.Count);
            foreach (var selectedSite in batch)
            {
                if (sitesByDomain.TryGetValue(selectedSite.Domain, out var site))
                {
                    availableSites.Add(site);
                }
                else
                {
                    AddSkippedItem(
                        run,
                        selectedSite,
                        "Site was no longer available when its Ahrefs batch was processed.");
                }
            }

            if (availableSites.Count > 0)
            {
                try
                {
                    var targets = availableSites
                        .Select(site => new AhrefsBatchTarget(
                            BuildTargetUrl(site.Domain),
                            run.TargetMode,
                            run.Protocol))
                        .ToList();
                    var result = await _apiClient.RunBatchAnalysisAsync(
                        targets,
                        run.VolumeMode,
                        cancellationToken);
                    var returnedRows = MapRowsByIndex(result.Rows, availableSites.Count);
                    var snapshots = saveSnapshots
                        ? await LoadSnapshotsAsync(run.SnapshotMonth, availableSites, cancellationToken)
                        : null;

                    for (var index = 0; index < availableSites.Count; index++)
                    {
                        var site = availableSites[index];
                        if (!returnedRows.TryGetValue(index, out var row))
                        {
                            AddNotReturnedItem(run, site, index);
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(row.Error))
                        {
                            AddFailedItem(run, site, index, row.Error);
                            continue;
                        }

                        if (row.OrganicTraffic == null || row.DomainRating == null)
                        {
                            AddFailedItem(run, site, index, "Ahrefs did not return both required metrics.");
                            continue;
                        }

                        ApplySuccessfulRow(run, site, row, snapshots);
                    }

                    var estimatedBatchUnits = AhrefsSyncCostCalculator.EstimateUnits(
                        availableSites.Count,
                        run.BatchSize);
                    run.ActualUnits += result.Cost.EffectiveUnits > 0
                        ? result.Cost.EffectiveUnits
                        : estimatedBatchUnits;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ex is AhrefsApiException { BatchResponseReceived: true } apiException)
                    {
                        run.ActualUnits += apiException.ActualUnits ??
                            AhrefsSyncCostCalculator.EstimateUnits(
                                availableSites.Count,
                                run.BatchSize);
                    }

                    foreach (var site in availableSites)
                    {
                        AddFailedItem(run, site, null, ex.Message);
                    }
                }
            }

            run.ProcessedSitesCount += batch.Count;
            await _context.SaveChangesAsync(cancellationToken);
            DetachCompletedBatch(run);

            var remaining = run.AvailableUnitsBefore - run.ActualUnits;
            if (remaining < run.StopIfRemainingUnitsBelow && offset + batch.Count < sites.Count)
            {
                run.Status = AhrefsSyncRunStatus.StoppedInsufficientUnits;
                run.ErrorMessage = "Sync stopped because remaining Ahrefs units fell below the configured threshold.";
                await SaveSkippedSitesAsync(
                    run,
                    sites.Skip(offset + batch.Count),
                    run.ErrorMessage,
                    cancellationToken);
                break;
            }
        }
    }

    private void ApplySuccessfulRow(
        AhrefsSyncRun run,
        Site site,
        AhrefsBatchRow row,
        IDictionary<string, SiteMetricSnapshot>? snapshots)
    {
        var item = CreateItem(run, site, AhrefsSyncRunItemStatus.Succeeded);
        item.AhrefsIndex = row.Index;
        item.NewTraffic = row.OrganicTraffic;
        item.NewDomainRating = row.DomainRating;

        site.Traffic = row.OrganicTraffic!.Value;
        site.DR = row.DomainRating!.Value;
        site.AhrefsLastSyncedAt = DateTime.UtcNow;

        if (snapshots != null)
        {
            if (!snapshots.TryGetValue(site.Domain, out var snapshot))
            {
                snapshot = new SiteMetricSnapshot
                {
                    Id = Guid.NewGuid(),
                    Domain = site.Domain,
                    SnapshotMonth = run.SnapshotMonth
                };
                _context.SiteMetricSnapshots.Add(snapshot);
                snapshots.Add(site.Domain, snapshot);
            }

            snapshot.Traffic = site.Traffic;
            snapshot.DomainRating = site.DR;
            snapshot.Source = SnapshotSource;
            snapshot.AhrefsSyncRunId = run.Id;
            snapshot.FetchedAt = site.AhrefsLastSyncedAt.Value;
            item.SnapshotSaved = true;
        }

        _context.AhrefsSyncRunItems.Add(item);
        run.UpdatedSitesCount++;
    }

    private async Task<Dictionary<string, SiteMetricSnapshot>> LoadSnapshotsAsync(
        DateOnly snapshotMonth,
        IReadOnlyCollection<Site> sites,
        CancellationToken cancellationToken)
    {
        var domains = sites.Select(site => site.Domain).ToList();
        return await _context.SiteMetricSnapshots
            .Where(snapshot =>
                snapshot.SnapshotMonth == snapshotMonth &&
                domains.Contains(snapshot.Domain))
            .ToDictionaryAsync(snapshot => snapshot.Domain, cancellationToken);
    }

    private void AddNotReturnedItem(AhrefsSyncRun run, Site site, int index)
    {
        var item = CreateItem(run, site, AhrefsSyncRunItemStatus.NotReturnedByAhrefs);
        item.AhrefsIndex = index;
        item.ErrorMessage = "Ahrefs did not return a row for this target.";
        _context.AhrefsSyncRunItems.Add(item);
        run.SkippedSitesCount++;
    }

    private void AddFailedItem(
        AhrefsSyncRun run,
        Site site,
        int? index,
        string errorMessage)
    {
        var item = CreateItem(run, site, AhrefsSyncRunItemStatus.Failed);
        item.AhrefsIndex = index;
        item.ErrorMessage = errorMessage;
        _context.AhrefsSyncRunItems.Add(item);
        run.FailedSitesCount++;
    }

    private void AddSkippedItem(
        AhrefsSyncRun run,
        SelectedSite site,
        string errorMessage)
    {
        var item = CreateItem(run, site, AhrefsSyncRunItemStatus.Skipped);
        item.ErrorMessage = errorMessage;
        _context.AhrefsSyncRunItems.Add(item);
        run.SkippedSitesCount++;
    }

    private static AhrefsSyncRunItem CreateItem(
        AhrefsSyncRun run,
        Site site,
        AhrefsSyncRunItemStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Domain = site.Domain,
            Status = status,
            OldTraffic = site.Traffic,
            OldDomainRating = site.DR,
            SnapshotMonth = run.SnapshotMonth
        };

    private static AhrefsSyncRunItem CreateItem(
        AhrefsSyncRun run,
        SelectedSite site,
        AhrefsSyncRunItemStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Domain = site.Domain,
            Status = status,
            OldTraffic = site.Traffic,
            OldDomainRating = site.DomainRating,
            SnapshotMonth = run.SnapshotMonth
        };

    private async Task SaveSkippedSitesAsync(
        AhrefsSyncRun run,
        IEnumerable<SelectedSite> sites,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        foreach (var batch in sites.Chunk(run.BatchSize))
        {
            foreach (var site in batch)
            {
                AddSkippedItem(run, site, errorMessage);
            }

            await _context.SaveChangesAsync(cancellationToken);
            DetachCompletedBatch(run);
        }
    }

    private void DetachCompletedBatch(AhrefsSyncRun run)
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (!ReferenceEquals(entry.Entity, run))
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<AhrefsSyncRun?> TryFinalizeFailedRunAsync(
        AhrefsSyncRun sourceRun,
        AhrefsSyncRunStatus status,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            _context.ChangeTracker.Clear();
            var persistedRun = await _context.AhrefsSyncRuns
                .FirstOrDefaultAsync(run => run.Id == sourceRun.Id, cancellationToken);
            if (persistedRun == null)
            {
                return null;
            }

            persistedRun.Status = status;
            persistedRun.ErrorMessage = errorMessage;
            persistedRun.FinishedAt = DateTime.UtcNow;
            persistedRun.UsageResetDate = sourceRun.UsageResetDate;
            persistedRun.EligibleSitesCount = sourceRun.EligibleSitesCount;
            persistedRun.SelectedSitesCount = sourceRun.SelectedSitesCount;
            persistedRun.FullEstimatedUnits = sourceRun.FullEstimatedUnits;
            persistedRun.SelectedEstimatedUnits = sourceRun.SelectedEstimatedUnits;
            persistedRun.WasLimitedByBudget = sourceRun.WasLimitedByBudget;
            persistedRun.AvailableUnitsBefore = sourceRun.AvailableUnitsBefore;
            persistedRun.ActualUnits = sourceRun.ActualUnits;
            persistedRun.AvailableUnitsAfter = Math.Max(
                0,
                sourceRun.AvailableUnitsBefore - sourceRun.ActualUnits);
            await _context.SaveChangesAsync(cancellationToken);
            return persistedRun;
        }
        catch (Exception finalizationException)
        {
            _logger.LogError(
                finalizationException,
                "Could not persist terminal Ahrefs sync status. RunId={RunId}; Status={Status}",
                sourceRun.Id,
                status);
            return null;
        }
    }

    private AhrefsSyncRun CreateRun(
        AhrefsSyncRequest request,
        DateTime startedAt,
        DateOnly snapshotMonth)
        => new()
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            Status = AhrefsSyncRunStatus.Running,
            RunKind = request.RunKind,
            TriggeredByUserId = request.TriggeredByUserId,
            Force = request.Force,
            SnapshotMonth = snapshotMonth,
            CostPerSite = AhrefsSyncCostCalculator.CostPerSite,
            SafetyBufferUnits = _options.SafetyBufferUnits,
            StopIfRemainingUnitsBelow = _options.StopIfRemainingUnitsBelow,
            BatchSize = _options.BatchSize,
            MaxSitesPerRun = Math.Min(
                request.MaxSitesOverride ?? _options.MaxSitesPerRun,
                _options.MaxSitesPerRun),
            TargetMode = _options.TargetMode,
            Protocol = _options.Protocol,
            VolumeMode = _options.VolumeMode
        };

    private Selection CalculateSelection(
        int eligibleCount,
        int maxSites,
        long availableUnits)
    {
        var spendableUnits = Math.Max(0, availableUnits - _options.SafetyBufferUnits);
        var affordableCount = AhrefsSyncCostCalculator.GetAffordableSiteCount(
            spendableUnits,
            eligibleCount,
            _options.BatchSize);
        var selectedCount = Math.Min(eligibleCount, Math.Min(maxSites, affordableCount));

        return new Selection(
            selectedCount,
            AhrefsSyncCostCalculator.EstimateUnits(eligibleCount, _options.BatchSize),
            AhrefsSyncCostCalculator.EstimateUnits(selectedCount, _options.BatchSize),
            affordableCount < eligibleCount);
    }

    private static void ApplySelection(AhrefsSyncRun run, Selection selection)
    {
        run.SelectedSitesCount = selection.SelectedSitesCount;
        run.FullEstimatedUnits = selection.FullEstimatedUnits;
        run.SelectedEstimatedUnits = selection.SelectedEstimatedUnits;
        run.WasLimitedByBudget = selection.WasLimitedByBudget;
    }

    private long GetEffectiveAvailableUnits(AhrefsLimitsAndUsage limits)
        => Math.Max(
            0,
            Math.Min(
                Math.Min(
                    limits.UnitsLimitApiKey - limits.UnitsUsageApiKey,
                    limits.UnitsLimitWorkspace - limits.UnitsUsageWorkspace),
                _options.MonthlyAppBudgetUnits - limits.UnitsUsageApiKey));

    private Task<bool> HasSuccessfulFullRunAsync(
        DateOnly snapshotMonth,
        CancellationToken cancellationToken)
        => _context.AhrefsSyncRuns.AsNoTracking().AnyAsync(
            run => run.SnapshotMonth == snapshotMonth &&
                run.Status == AhrefsSyncRunStatus.Succeeded &&
                run.IsFullCoverage,
            cancellationToken);

    private static bool IsFullRun(AhrefsSyncRunKind runKind)
        => runKind is AhrefsSyncRunKind.Scheduled or AhrefsSyncRunKind.ManualFull;

    internal static string BuildTargetUrl(string domain)
        => new UriBuilder(Uri.UriSchemeHttps, domain).Uri.GetLeftPart(UriPartial.Authority);

    private static IReadOnlyDictionary<int, AhrefsBatchRow> MapRowsByIndex(
        IReadOnlyList<AhrefsBatchRow> rows,
        int targetCount)
    {
        var result = new Dictionary<int, AhrefsBatchRow>();
        foreach (var row in rows)
        {
            if (row.Index < 0 || row.Index >= targetCount)
            {
                throw new InvalidOperationException(
                    $"Ahrefs returned index {row.Index} outside the target range.");
            }

            if (!result.TryAdd(row.Index, row))
            {
                throw new InvalidOperationException(
                    $"Ahrefs returned duplicate index {row.Index}.");
            }
        }

        return result;
    }

    internal static DateOnly GetSnapshotMonth(DateTime utcNow)
        => new(utcNow.Year, utcNow.Month, 1);

    private sealed record Selection(
        int SelectedSitesCount,
        long FullEstimatedUnits,
        long SelectedEstimatedUnits,
        bool WasLimitedByBudget);

    private sealed record SelectedSite(
        string Domain,
        long Traffic,
        double DomainRating);
}
