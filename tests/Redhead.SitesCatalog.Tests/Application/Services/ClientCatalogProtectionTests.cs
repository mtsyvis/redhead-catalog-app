using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.DependencyInjection;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Locations;

namespace Redhead.SitesCatalog.Tests;

public sealed class ClientCatalogProtectionTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private const string UserId = "client";

    public ClientCatalogProtectionTests()
    {
        _db.Users.Add(new ApplicationUser { Id = UserId, Email = "client@example.com" });
        _db.RoleSettings.AddRange(
            new RoleSettings { RoleName = AppRoles.Client, ExportLimitMode = ExportLimitMode.Unlimited },
            new RoleSettings { RoleName = AppRoles.Internal, ExportLimitMode = ExportLimitMode.Unlimited });
        _db.Sites.AddRange(Enumerable.Range(1, 350).Select(i => new Site
        {
            Domain = $"site{i:000}.com", DR = 50, Traffic = i, Location = "US"
        }));
        _db.SaveChanges();
    }

    [Theory]
    [InlineData(1, 25, 100)]
    [InlineData(2, 100, 100)]
    [InlineData(int.MaxValue, int.MaxValue, 100)]
    [InlineData(-1, -1, 100)]
    [InlineData(8, 1000, 300)]
    public async Task Search_ClientAlwaysGetsFirstSelection_WithFullTotal(int page, int pageSize, int limit)
    {
        // Arrange
        _db.Users.Single().ClientSelectionLimitOverride = limit;
        await _db.SaveChangesAsync();
        var sut = CreateController();

        // Act
        var result = await sut.SearchSites(new SitesQueryRequest { Page = page, PageSize = pageSize }, CancellationToken.None);

        // Assert
        var response = Assert.IsType<SitesListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(350, response.Total);
        Assert.Equal(limit, response.SelectionLimit);
        Assert.Equal(limit, response.Items.Count);
        Assert.Equal("site001.com", response.Items.First().Domain);
        Assert.Equal($"site{limit:000}.com", response.Items.Last().Domain);
    }

    [Fact]
    public async Task Search_InternalPagingAndStopListRemainAvailable()
    {
        // Arrange
        var sut = CreateController(AppRoles.Internal);

        // Act
        var result = await sut.SearchSites(new SitesQueryRequest { Page = 2, PageSize = 100, StopListDomains = ["site001.com"] }, CancellationToken.None);

        // Assert
        var response = Assert.IsType<SitesListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(response.SelectionLimit);
        Assert.Equal(349, response.Total);
        Assert.Equal("site102.com", response.Items.First().Domain);
    }

    [Theory]
    [InlineData(100, null, null, 1)]
    [InlineData(300, 50, null, 10)]
    [InlineData(100, 50, 2, 1)]
    [InlineData(100, 50, null, 348)]
    public async Task ClientStopList_ExcludesBeforeSelection_AndPreservesExportLimits(
        int selectionLimit, int? exportLimit, int? dailyLimit, int excludedCount)
    {
        // Arrange
        _db.Users.Single().ClientSelectionLimitOverride = selectionLimit;
        var settings = _db.RoleSettings.Single(x => x.RoleName == AppRoles.Client);
        settings.ExportLimitMode = exportLimit.HasValue ? ExportLimitMode.Limited : ExportLimitMode.Unlimited;
        settings.ExportLimitRows = exportLimit;
        settings.DailyUniqueExportedDomainsLimit = dailyLimit;
        await _db.SaveChangesAsync();
        var excluded = Enumerable.Range(1, excludedCount).Select(i => $"https://www.site{i:000}.com/path")
            .Append("site001.com").ToList();
        var query = new SitesQuery { StopListDomains = excluded, SortBy = "domain" };
        var export = CreateExport();

        // Act
        var search = await CreateController().SearchSites(new SitesQueryRequest { StopListDomains = excluded }, CancellationToken.None);
        var preview = await export.PreviewAsync(query, null, UserId, AppRoles.Client);
        var drive = await export.PrepareSitesExportAsync(query, UserId, "client@example.com", AppRoles.Client, ["domain"], ExportConstants.DestinationGoogleDrive);
        var excel = await export.ExportSitesAsExcelAsync(query, UserId, "client@example.com", AppRoles.Client, ["domain"]);

        // Assert
        var response = Assert.IsType<SitesListResponse>(Assert.IsType<OkObjectResult>(search.Result).Value);
        var expectedSelection = Math.Min(selectionLimit, 350 - excludedCount);
        var expectedExport = Math.Min(expectedSelection, Math.Min(exportLimit ?? int.MaxValue, dailyLimit ?? int.MaxValue));
        Assert.Equal(350 - excludedCount, response.Total);
        Assert.Equal(expectedSelection, response.Items.Count);
        Assert.Equal($"site{excludedCount + 1:000}.com", response.Items.First().Domain);
        Assert.Equal(expectedSelection, preview.SelectionRows);
        Assert.Equal(expectedExport, preview.ExportableRows);
        Assert.Equal(expectedExport, drive.ExportedRows);
        Assert.Equal(expectedExport, excel.ExportedRows);
        var expectedDomains = response.Items.Take(expectedExport).Select(x => x.Domain).ToArray();
        Assert.Equal(expectedDomains, XlsxTestWorkbook.ReadRows(drive.FileStream, "Sites").Select(x => x["Domain"]));
        Assert.Equal(expectedDomains, XlsxTestWorkbook.ReadRows(excel.FileStream, "Sites").Select(x => x["Domain"]));
    }

    [Theory]
    [InlineData("search")]
    [InlineData("export")]
    [InlineData("preview")]
    public async Task MultiSearch_RejectsMoreThanPersonalLimitAcrossDataPaths(string path)
    {
        // Arrange
        var input = string.Join('\n', Enumerable.Range(1, 101).Select(i => $"site{i:000}.com"));
        var export = CreateExport();

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(async () =>
        {
            if (path == "search")
                await CreateController().MultiSearch(new MultiSearchRequest { QueryText = input }, CancellationToken.None);
            else if (path == "preview")
                await export.PreviewAsync(new SitesQuery(), input, UserId, AppRoles.Client);
            else
                await export.PrepareMultiSearchExportAsync(input, new SitesQuery(), UserId, "client@example.com", AppRoles.Client, ["domain"], ExportConstants.DestinationGoogleDrive);
        });

        // Assert
        Assert.Contains("100 unique domains", exception.Message);
        Assert.Empty(_db.ExportLogs);
    }

    [Fact]
    public async Task MultiSearch_NormalizesDuplicates_AndAllowsRepeatedSearches()
    {
        // Arrange
        _db.Users.Single().ClientSelectionLimitOverride = 1;
        await _db.SaveChangesAsync();
        var sut = CreateController();
        var request = new MultiSearchRequest { QueryText = "site001.com https://www.site001.com/path" };

        // Act
        var first = await sut.MultiSearch(request, CancellationToken.None);
        var second = await sut.MultiSearch(request, CancellationToken.None);

        // Assert
        foreach (var result in new[] { first, second })
            Assert.Single(Assert.IsType<MultiSearchResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Found);
        Assert.Empty(_db.LiteMultiSearchUsages);
    }

    [Theory]
    [InlineData(null, null, 100)]
    [InlineData(300, 50, 50)]
    [InlineData(300, 1000, 300)]
    public async Task Export_PreservesSmallerLimit_AndNeverAdvancesSelection(int? selection, int? exportRows, int expected)
    {
        // Arrange
        _db.Users.Single().ClientSelectionLimitOverride = selection;
        var settings = _db.RoleSettings.Single(x => x.RoleName == AppRoles.Client);
        settings.ExportLimitMode = exportRows.HasValue ? ExportLimitMode.Limited : ExportLimitMode.Unlimited;
        settings.ExportLimitRows = exportRows;
        await _db.SaveChangesAsync();
        var sut = CreateExport();
        var query = new SitesQuery { Page = int.MaxValue, PageSize = int.MaxValue, SortBy = "dr" };

        // Act
        var first = await sut.ExportSitesAsExcelAsync(query, UserId, "client@example.com", AppRoles.Client, ["domain"]);
        var next = await sut.ExportSitesAsExcelAsync(query, UserId, "client@example.com", AppRoles.Client, ["domain"]);

        // Assert
        Assert.Equal(expected, first.ExportedRows);
        Assert.Equal(expected, next.ExportedRows);
        var firstRows = XlsxTestWorkbook.ReadRows(first.FileStream, "Sites");
        var nextRows = XlsxTestWorkbook.ReadRows(next.FileStream, "Sites");
        Assert.Equal(firstRows.Select(x => x["Domain"]), nextRows.Select(x => x["Domain"]));
        Assert.Equal("site001.com", firstRows[0]["Domain"]);
        Assert.Equal($"site{expected:000}.com", firstRows.Last()["Domain"]);
    }

    [Fact]
    public async Task Preview_DoesNotConsumeQuota_AndPartialExportStaysInsideSelection()
    {
        // Arrange
        var settings = _db.RoleSettings.Single(x => x.RoleName == AppRoles.Client);
        settings.DailyUniqueExportedDomainsLimit = 40;
        await _db.SaveChangesAsync();
        var sut = CreateExport();

        // Act
        var preview = await sut.PreviewAsync(new SitesQuery(), null, UserId, AppRoles.Client);
        var logCountBeforeExport = await _db.ExportLogs.CountAsync();
        var first = await sut.ExportSitesAsExcelAsync(new SitesQuery(), UserId, "client@example.com", AppRoles.Client, ["domain"]);
        var second = await sut.ExportSitesAsExcelAsync(new SitesQuery(), UserId, "client@example.com", AppRoles.Client, ["domain"]);

        // Assert
        Assert.Equal(100, preview.SelectionRows);
        Assert.Equal(40, preview.ExportableRows);
        Assert.False(preview.IsBlocked);
        Assert.Equal(0, logCountBeforeExport);
        Assert.Equal(40, first.ExportedRows);
        Assert.Equal(40, second.ExportedRows);
        Assert.Equal(40, await _db.ExportedDomainAccesses.Select(x => x.Domain).Distinct().CountAsync());
    }

    [Theory]
    [InlineData(AppRoles.Client, 100)]
    [InlineData(AppRoles.Internal, 350)]
    public async Task GoogleDrivePreparation_UsesSameSelectionCap(string role, int expected)
    {
        // Arrange
        var sut = CreateExport();

        // Act
        var prepared = await sut.PrepareSitesExportAsync(new SitesQuery(), UserId, "client@example.com", role, ["domain"], ExportConstants.DestinationGoogleDrive);

        // Assert
        Assert.Equal(expected, prepared.ExportedRows);
        Assert.Equal(expected, XlsxTestWorkbook.ReadRows(prepared.FileStream, "Sites").Count);
        Assert.Empty(_db.ExportLogs);
    }

    [Fact]
    public void RateLimit_IsSharedAcrossSessionsAndIps_ButNotUsersOrInternalRoles()
    {
        // Arrange
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            ClientCatalogServiceCollectionExtensions.CreatePartition(context, 2));
        var first = CreateHttpContext();
        var otherSession = CreateHttpContext();
        otherSession.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.7");

        // Act
        using var one = limiter.AttemptAcquire(first);
        using var two = limiter.AttemptAcquire(otherSession);
        using var rejected = limiter.AttemptAcquire(first);
        using var otherUser = limiter.AttemptAcquire(CreateHttpContext(userId: "another"));
        using var internalUser = limiter.AttemptAcquire(CreateHttpContext(AppRoles.Internal), 100);

        // Assert
        Assert.True(one.IsAcquired);
        Assert.True(two.IsAcquired);
        Assert.False(rejected.IsAcquired);
        Assert.True(otherUser.IsAcquired);
        Assert.True(internalUser.IsAcquired);
    }

    [Fact]
    public void SelectionConfiguration_RequiresUsersManagePermission()
    {
        // Arrange
        var type = typeof(ClientSelectionLimitController);

        // Act
        var attribute = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());

        // Assert
        Assert.Equal(AppPolicies.UsersManageAccess, attribute.Policy);
        Assert.True(RolePermissionMatrix.HasPermission(AppRoles.SuperAdmin, AppPermissions.UsersManage));
        Assert.False(RolePermissionMatrix.HasPermission(AppRoles.Client, AppPermissions.UsersManage));
        Assert.False(RolePermissionMatrix.HasPermission(AppRoles.Admin, AppPermissions.UsersManage));
    }

    private SitesController CreateController(string role = AppRoles.Client)
        => new(new SitesService(_db, new SitesQueryBuilder(_db), new SitesCatalogCache(_cache), new LocationNormalizer()),
            Mock.Of<ILiteMultiSearchUsageService>(), new ClientCatalogService(_db))
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(role) }
        };

    private ExportService CreateExport(IClientCatalogService? limits = null) => new(_db, new SitesQueryBuilder(_db), new EffectiveExportPolicyService(_db),
        new ExportUsageLimitService(_db), new SitesExcelExportGenerator(), limits ?? new ClientCatalogService(_db));

    [Theory]
    [InlineData("preview", false)]
    [InlineData("preview", true)]
    [InlineData("export", false)]
    [InlineData("export", true)]
    public async Task SelectionLimit_IsReadOncePerOperation(string operation, bool multiSearch)
    {
        // Arrange
        var reads = 0;
        var limits = new Mock<IClientCatalogService>();
        limits.Setup(x => x.GetSelectionLimitAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++reads == 1 ? 100 : 1);
        var sut = CreateExport(limits.Object);
        var input = string.Join('\n', Enumerable.Range(1, 100).Select(i => $"site{i:000}.com"));
        var query = new SitesQuery();

        // Act
        var count = operation == "preview"
            ? (await sut.PreviewAsync(query, multiSearch ? input : null, UserId, AppRoles.Client)).ExportableRows
            : multiSearch
                ? (await sut.PrepareMultiSearchExportAsync(input, query, UserId, "client@example.com", AppRoles.Client, ["domain"], ExportConstants.DestinationGoogleDrive)).ExportedRows
                : (await sut.PrepareSitesExportAsync(query, UserId, "client@example.com", AppRoles.Client, ["domain"], ExportConstants.DestinationGoogleDrive)).ExportedRows;

        // Assert
        Assert.Equal(100, count);
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task MultiSearchPreview_MatchesExport_WithFiltersLimitsAndNotFound(string direction)
    {
        // Arrange
        _db.Users.Single().ClientSelectionLimitOverride = 150;
        var settings = _db.RoleSettings.Single(x => x.RoleName == AppRoles.Client);
        settings.ExportLimitMode = ExportLimitMode.Limited;
        settings.ExportLimitRows = 50;
        await _db.SaveChangesAsync();
        var input = string.Join('\n', Enumerable.Range(1, 120).Select(i => $"site{i:000}.com").Append("missing.com"));
        var query = new SitesQuery { TrafficMax = 80, SortBy = "traffic", SortDir = direction };
        var sut = CreateExport();

        // Act
        var preview = await sut.PreviewAsync(query, input, UserId, AppRoles.Client);
        var export = await sut.PrepareMultiSearchExportAsync(input, query, UserId, "client@example.com",
            AppRoles.Client, ["domain"], ExportConstants.DestinationGoogleDrive);

        // Assert
        Assert.Equal(80, preview.SelectionRows);
        Assert.Equal(export.RequestedRows, preview.SelectionRows);
        Assert.Equal(50, preview.ExportableRows);
        Assert.Equal(export.ExportedRows, preview.ExportableRows);
        Assert.Equal(1, preview.NotFoundRows);
        Assert.Single(XlsxTestWorkbook.ReadRows(export.FileStream, "Not found"));
        var rows = XlsxTestWorkbook.ReadRows(export.FileStream, "Sites");
        Assert.Equal(direction == "asc" ? "site001.com" : "site080.com", rows[0]["Domain"]);
        Assert.Empty(_db.ExportLogs);
    }

    private static DefaultHttpContext CreateHttpContext(string role = AppRoles.Client, string userId = UserId)
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "Test")) };

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }
}
