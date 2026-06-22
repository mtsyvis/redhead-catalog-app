using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Concurrency;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Ahrefs;

public sealed class AhrefsSyncServiceTests
{
    [Fact]
    public async Task DryRun_DoesNotCallBatchOrModifySitesOrSnapshots()
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("example.com", traffic: 100, dr: 20);
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.DryRunAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.CanRun);
        Assert.Equal(1, result.SelectedSitesCount);
        api.Verify(
            client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(100, site.Traffic);
        Assert.Equal(20, site.DR);
        Assert.Empty(context.SiteMetricSnapshots);
    }

    [Fact]
    public async Task DryRun_WhenNothingIsAffordable_ReturnsCannotRun()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 25_049);
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.DryRunAsync(null, CancellationToken.None);

        // Assert
        Assert.False(result.CanRun);
        Assert.Equal(0, result.SelectedSitesCount);
        Assert.NotNull(result.ReasonIfCannotRun);
    }

    [Fact]
    public async Task DryRun_WhenFullSetDoesNotFit_SelectsAffordableSites()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            Enumerable.Range(1, 20)
                .Select(index => CreateSite($"site-{index}.example")));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 25_120);
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.DryRunAsync(null, CancellationToken.None);

        // Assert
        Assert.True(result.CanRun);
        Assert.Equal(10, result.SelectedSitesCount);
        Assert.Equal(120, result.SelectedEstimatedUnits);
        Assert.True(result.WasLimitedByBudget);
    }

    [Fact]
    public async Task DryRun_WhenFullRunAlreadyCompleted_ReturnsCannotRun()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        context.AhrefsSyncRuns.Add(new AhrefsSyncRun
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-1),
            FinishedAt = DateTime.UtcNow.AddDays(-1),
            Status = AhrefsSyncRunStatus.Succeeded,
            RunKind = AhrefsSyncRunKind.Scheduled,
            IsFullCoverage = true,
            SnapshotMonth = CurrentSnapshotMonth(),
            TargetMode = "subdomains",
            Protocol = "both",
            VolumeMode = "monthly"
        });
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.DryRunAsync(null, CancellationToken.None);

        // Assert
        Assert.False(result.CanRun);
        Assert.Contains("already exists", result.ReasonIfCannotRun);
    }

    [Fact]
    public async Task DryRun_WhenSyncLockIsUnavailable_ReturnsCannotRun()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        var sut = CreateService(context, api.Object, syncLock: new UnavailableLock());

        // Act
        var result = await sut.DryRunAsync(null, CancellationToken.None);

        // Assert
        Assert.False(result.CanRun);
        Assert.Contains("already running", result.ReasonIfCannotRun);
        api.Verify(
            client => client.GetLimitsAndUsageAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_SelectsTopTwoNonQuarantinedSitesInTrafficThenDomainOrder()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            CreateSite("excluded-quarantined.example", traffic: 1000, quarantined: true),
            CreateSite("selected-first.example", traffic: 200),
            CreateSite("selected-second.example", traffic: 200),
            CreateSite("excluded-by-limit.example", traffic: 100));
        await context.SaveChangesAsync();

        // Three sites are eligible. MaxSitesPerRun=2 means only the first two ordered
        // by Traffic DESC, Domain ASC must be sent to Ahrefs.
        IReadOnlyList<AhrefsBatchTarget>? capturedTargets = null;
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AhrefsBatchTarget>, string, CancellationToken>(
                (targets, _, _) => capturedTargets = targets)
            .ReturnsAsync(new AhrefsBatchResult(
                [
                    new AhrefsBatchRow(0, 1, 1),
                    new AhrefsBatchRow(1, 2, 2)
                ],
                new AhrefsBatchCost(2, 12, 50, 50, null)));
        var sut = CreateService(
            context,
            api.Object,
            new AhrefsSyncOptions { MaxSitesPerRun = 2 });

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualFull,
                "user-1",
                null,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.False(result.Conflict);
        Assert.Equal(3, result.Run!.EligibleSitesCount);
        Assert.Equal(2, result.Run.SelectedSitesCount);
        Assert.NotNull(capturedTargets);
        var targets = capturedTargets!;
        Assert.Equal(
            [
                "https://selected-first.example",
                "https://selected-second.example"
            ],
            targets.Select(target => target.Url));
        Assert.All(targets, target => Assert.Equal("subdomains", target.Mode));
    }

    [Fact]
    public async Task Run_MapsReorderedAhrefsRowsToTargetsByIndex()
    {
        // Arrange
        await using var context = CreateContext();
        var oldUpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var indexZeroSite = CreateSite("index-zero.example", traffic: 200, dr: 10);
        var indexOneSite = CreateSite("index-one.example", traffic: 100, dr: 20);
        indexOneSite.UpdatedAtUtc = oldUpdatedAt;
        indexOneSite.UpdatedBy = "manager@example.com";
        context.Sites.AddRange(indexZeroSite, indexOneSite);
        await context.SaveChangesAsync();

        IReadOnlyList<AhrefsBatchTarget>? capturedTargets = null;
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AhrefsBatchTarget>, string, CancellationToken>(
                (targets, _, _) => capturedTargets = targets)
            .ReturnsAsync(new AhrefsBatchResult(
                [
                    // Ahrefs deliberately returns index 1 before index 0.
                    new AhrefsBatchRow(1, 0, 77),
                    new AhrefsBatchRow(0, 500, 66)
                ],
                new AhrefsBatchCost(2, 12, 50, 50, null)));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualFull,
                "user-1",
                null,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedTargets);
        Assert.Collection(
            capturedTargets!,
            target => Assert.Equal("https://index-zero.example", target.Url),
            target => Assert.Equal("https://index-one.example", target.Url));

        // Response row index=0 must update the first request target.
        Assert.Equal(500, indexZeroSite.Traffic);
        Assert.Equal(66, indexZeroSite.DR);

        // Response row index=1 must update the second request target.
        // Zero traffic is a valid value and must be persisted.
        Assert.Equal(0, indexOneSite.Traffic);
        Assert.Equal(77, indexOneSite.DR);
        Assert.NotNull(indexOneSite.AhrefsLastSyncedAt);
        Assert.Equal(oldUpdatedAt, indexOneSite.UpdatedAtUtc);
        Assert.Equal("manager@example.com", indexOneSite.UpdatedBy);
        Assert.Equal(2, result.Run!.UpdatedSitesCount);
    }

    [Fact]
    public async Task Run_MissingOrInvalidRowsPreserveExistingValuesAndCreateAuditItems()
    {
        // Arrange
        await using var context = CreateContext();
        var missing = CreateSite("missing.example", traffic: 200, dr: 20);
        var invalid = CreateSite("invalid.example", traffic: 100, dr: 10);
        context.Sites.AddRange(missing, invalid);
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsBatchResult(
                [new AhrefsBatchRow(1, 999, null)],
                new AhrefsBatchCost(2, 12, 50, 50, null)));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualFull,
                null,
                null,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.Equal(200, missing.Traffic);
        Assert.Equal(20, missing.DR);
        Assert.Equal(100, invalid.Traffic);
        Assert.Equal(10, invalid.DR);
        var items = await context.AhrefsSyncRunItems
            .Where(item => item.RunId == result.Run!.Id)
            .ToListAsync();
        Assert.Contains(
            items,
            item => item.Domain == "missing.example" &&
                item.Status == AhrefsSyncRunItemStatus.NotReturnedByAhrefs);
        Assert.Contains(
            items,
            item => item.Domain == "invalid.example" &&
                item.Status == AhrefsSyncRunItemStatus.Failed);
    }

    [Fact]
    public async Task Run_UpsertsSnapshotForSameDomainAndMonth()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.SetupSequence(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchResult(10, 20))
            .ReturnsAsync(BatchResult(30, 40));
        var sut = CreateService(context, api.Object);

        // Act
        await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);
        await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);

        // Assert
        var snapshot = Assert.Single(context.SiteMetricSnapshots);
        Assert.Equal(30, snapshot.Traffic);
        Assert.Equal(40, snapshot.DomainRating);
        Assert.Equal(AhrefsSyncService.SnapshotSource, snapshot.Source);
    }

    [Fact]
    public async Task FullRunGuard_SkipsUnlessForceIsTrue()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        context.AhrefsSyncRuns.Add(new AhrefsSyncRun
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-1),
            FinishedAt = DateTime.UtcNow.AddDays(-1),
            Status = AhrefsSyncRunStatus.Succeeded,
            RunKind = AhrefsSyncRunKind.Scheduled,
            IsFullCoverage = true,
            SnapshotMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
            TargetMode = "subdomains",
            Protocol = "both",
            VolumeMode = "monthly"
        });
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchResult(10, 20));
        var sut = CreateService(context, api.Object);

        // Act
        var skipped = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualFull,
                "user-1",
                null,
                SaveSnapshots: true,
                Force: false),
            CancellationToken.None);
        var forced = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualFull,
                "user-1",
                null,
                SaveSnapshots: true,
                Force: true),
            CancellationToken.None);

        // Assert
        Assert.Equal(AhrefsSyncRunStatus.SkippedAlreadyCompleted, skipped.Run!.Status);
        Assert.True(forced.Run!.Force);
        Assert.Equal("user-1", forced.Run.TriggeredByUserId);
        api.Verify(
            client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ManualLimitedRun_NeverMarksFullCoverage()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchResult(10, 20));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: false,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.False(result.Run!.IsFullCoverage);
        Assert.Empty(context.SiteMetricSnapshots);
    }

    [Theory]
    [InlineData(-1, "outside the target range")]
    [InlineData(1, "outside the target range")]
    public async Task Run_WhenAhrefsIndexIsOutsideTargetRange_FailsBatchWithoutUpdatingSite(
        int index,
        string expectedError)
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("example.com", traffic: 100, dr: 20);
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsBatchResult(
                [new AhrefsBatchRow(index, 200, 40)],
                new AhrefsBatchCost(1, 12, 50, 50, null)));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: false,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.Equal(100, site.Traffic);
        var item = await context.AhrefsSyncRunItems.SingleAsync(
            candidate => candidate.RunId == result.Run!.Id);
        Assert.Equal(AhrefsSyncRunItemStatus.Failed, item.Status);
        Assert.Contains(expectedError, item.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    public async Task Run_WhenAhrefsIndexIsDuplicated_FailsBatchWithoutUpdatingSite(int index)
    {
        // Arrange
        await using var context = CreateContext();
        var site = CreateSite("example.com", traffic: 100, dr: 20);
        context.Sites.Add(site);
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsBatchResult(
                [
                    new AhrefsBatchRow(index, 200, 40),
                    new AhrefsBatchRow(index, 300, 50)
                ],
                new AhrefsBatchCost(1, 12, 50, 50, null)));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: false,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.Equal(100, site.Traffic);
        var item = await context.AhrefsSyncRunItems.SingleAsync(
            candidate => candidate.RunId == result.Run!.Id);
        Assert.Equal(AhrefsSyncRunItemStatus.Failed, item.Status);
        Assert.Contains("duplicate index 0", item.ErrorMessage);
    }

    [Fact]
    public async Task Run_WhenSuccessfulBatchResponseCannotBeParsed_RecordsHeaderCost()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.Add(CreateSite("example.com"));
        await context.SaveChangesAsync();
        var api = CreateApiMock(availableUnits: 100_000);
        api.Setup(client => client.RunBatchAnalysisAsync(
                It.IsAny<IReadOnlyList<AhrefsBatchTarget>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AhrefsApiException(
                "Invalid response.",
                actualUnits: 73,
                batchResponseReceived: true));
        var sut = CreateService(context, api.Object);

        // Act
        var result = await sut.RunAsync(
            new AhrefsSyncRequest(
                AhrefsSyncRunKind.ManualLimited,
                null,
                1,
                SaveSnapshots: false,
                Force: false),
            CancellationToken.None);

        // Assert
        Assert.Equal(73, result.Run!.ActualUnits);
        Assert.Equal(AhrefsSyncRunStatus.Failed, result.Run.Status);
    }

    [Fact]
    public async Task GetRun_ReturnsRequestedItemsPage()
    {
        // Arrange
        await using var context = CreateContext();
        var run = new AhrefsSyncRun
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            Status = AhrefsSyncRunStatus.SucceededPartial,
            RunKind = AhrefsSyncRunKind.ManualLimited,
            SnapshotMonth = CurrentSnapshotMonth(),
            TargetMode = "subdomains",
            Protocol = "both",
            VolumeMode = "monthly"
        };
        context.AhrefsSyncRuns.Add(run);
        context.AhrefsSyncRunItems.AddRange(
            CreateRunItem(run.Id, "a.example"),
            CreateRunItem(run.Id, "b.example"),
            CreateRunItem(run.Id, "c.example"));
        await context.SaveChangesAsync();
        var sut = CreateService(context, CreateApiMock(100_000).Object);

        // Act
        var result = await sut.GetRunAsync(run.Id, page: 2, pageSize: 2, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("c.example", Assert.Single(result.Items).Domain);
    }

    [Fact]
    public async Task ListRuns_ReturnsRequestedPage()
    {
        // Arrange
        await using var context = CreateContext();
        var now = DateTime.UtcNow;
        context.AhrefsSyncRuns.AddRange(
            CreateRun(now.AddMinutes(-1)),
            CreateRun(now.AddMinutes(-2)),
            CreateRun(now.AddMinutes(-3)));
        await context.SaveChangesAsync();
        var sut = CreateService(context, CreateApiMock(100_000).Object);

        // Act
        var result = await sut.ListRunsAsync(page: 2, pageSize: 2, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetMonitoringData_ReturnsBudgetBreakdownAndActiveRun()
    {
        // Arrange
        await using var context = CreateContext();
        context.Sites.AddRange(
            CreateSite("available.example"),
            CreateSite("quarantined.example", quarantined: true));
        var activeRun = CreateRun(DateTime.UtcNow);
        activeRun.Status = AhrefsSyncRunStatus.Running;
        context.AhrefsSyncRuns.Add(activeRun);
        await context.SaveChangesAsync();
        var api = new Mock<IAhrefsApiClient>();
        api.Setup(client => client.GetLimitsAndUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsLimitsAndUsage(
                UnitsLimitWorkspace: 1_000,
                UnitsUsageWorkspace: 100,
                UnitsLimitApiKey: 975,
                UnitsUsageApiKey: 75,
                UsageResetDate: DateTime.UtcNow.AddDays(10)));
        var options = new AhrefsSyncOptions
        {
            MonthlyAppBudgetUnits = 800,
            SafetyBufferUnits = 25
        };
        var sut = CreateService(context, api.Object, options);

        // Act
        var result = await sut.GetMonitoringDataAsync(
            refreshLimits: true,
            CancellationToken.None);

        // Assert
        Assert.Equal(1, result.EligibleSitesCount);
        Assert.Equal(50, result.FullEstimatedUnits);
        Assert.Equal(900, result.ApiKeyRemainingUnits);
        Assert.Equal(900, result.WorkspaceRemainingUnits);
        Assert.Equal(725, result.AppBudgetRemainingUnits);
        Assert.Equal(725, result.EffectiveAvailableUnits);
        Assert.Equal(25, result.SafetyBufferUnits);
        Assert.Equal(activeRun.Id, result.ActiveRun!.Id);
    }

    private static AhrefsSyncService CreateService(
        ApplicationDbContext context,
        IAhrefsApiClient apiClient,
        AhrefsSyncOptions? options = null,
        IAhrefsSyncLock? syncLock = null)
        => new(
            context,
            apiClient,
            new PassThroughLimitsProvider(apiClient),
            syncLock ?? new AlwaysAvailableLock(),
            Options.Create(options ?? new AhrefsSyncOptions()),
            NullLogger<AhrefsSyncService>.Instance);

    private static Mock<IAhrefsApiClient> CreateApiMock(long availableUnits)
    {
        var api = new Mock<IAhrefsApiClient>();
        api.Setup(client => client.GetLimitsAndUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AhrefsLimitsAndUsage(
                availableUnits,
                0,
                availableUnits,
                0,
                DateTime.UtcNow.AddMonths(1)));
        return api;
    }

    private static AhrefsBatchResult BatchResult(long traffic, double dr)
        => new(
            [new AhrefsBatchRow(0, traffic, dr)],
            new AhrefsBatchCost(1, 12, 50, 50, null));

    private static Site CreateSite(
        string domain,
        long traffic = 1,
        double dr = 1,
        bool quarantined = false)
        => new()
        {
            Domain = domain,
            Traffic = traffic,
            DR = dr,
            Location = "US",
            IsQuarantined = quarantined,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static AhrefsSyncRunItem CreateRunItem(Guid runId, string domain)
        => new()
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Domain = domain,
            Status = AhrefsSyncRunItemStatus.Succeeded,
            SnapshotMonth = CurrentSnapshotMonth()
        };

    private static AhrefsSyncRun CreateRun(DateTime startedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            Status = AhrefsSyncRunStatus.SucceededPartial,
            RunKind = AhrefsSyncRunKind.ManualLimited,
            SnapshotMonth = CurrentSnapshotMonth(),
            TargetMode = "subdomains",
            Protocol = "both",
            VolumeMode = "monthly"
        };

    private static DateOnly CurrentSnapshotMonth()
        => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class AlwaysAvailableLock : IAhrefsSyncLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable?>(new Handle());

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class UnavailableLock : IAhrefsSyncLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable?>(null);
    }

    private sealed class PassThroughLimitsProvider : IAhrefsLimitsProvider
    {
        private readonly IAhrefsApiClient _apiClient;

        public PassThroughLimitsProvider(IAhrefsApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<AhrefsLimitsSnapshot> GetAsync(
            bool forceRefresh,
            CancellationToken cancellationToken)
            => new(
                await _apiClient.GetLimitsAndUsageAsync(cancellationToken),
                DateTime.UtcNow);
    }
}
