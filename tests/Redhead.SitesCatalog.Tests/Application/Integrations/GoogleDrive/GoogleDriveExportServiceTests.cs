using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Infrastructure.Options;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Tests;

namespace Redhead.SitesCatalog.Tests.Application.Integrations.GoogleDrive;

public sealed class GoogleDriveExportServiceTests
{
    private const string TestUserId = "user-1";
    private const string TestUserEmail = "user@example.com";

    [Fact]
    public async Task ExportSitesAsync_WhenNoActiveConnectionExists_ReturnsNotConnectedError()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Unlimited);
        var driveClient = CreateDriveClientMock();
        var sut = CreateService(db, driveClient.Object);

        // Act
        var act = () => sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<GoogleDriveExportException>(act);
        Assert.Equal(GoogleDriveExportException.NotConnectedErrorCode, ex.ErrorCode);
    }

    [Fact]
    public async Task ExportSitesAsync_RespectsExportPermissionRules()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Disabled);
        SeedConnection(db);
        var driveClient = CreateDriveClientMock();
        var sut = CreateService(db, driveClient.Object);

        // Act
        var act = () => sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ExportDisabledException>(
            act);
    }

    [Fact]
    public async Task ExportSitesAsync_UploadsWorkbookAndReturnsMetadataWithoutTokens()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Limited, limitRows: 1);
        SeedConnection(db, exportFolderId: null);
        string? uploadContentType = null;
        long uploadedBytes = 0;
        var driveClient = CreateDriveClientMock();
        driveClient
            .Setup(client => client.UploadFileAsync(
                It.IsAny<string>(),
                "folder-created",
                ExportConstants.SitesFileName,
                It.IsAny<Stream>(),
                ExportConstants.ExcelContentType,
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string, Stream, string, CancellationToken>(async (_, _, fileName, content, contentType, _) =>
            {
                uploadContentType = contentType;
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy);
                uploadedBytes = copy.Length;

                return new GoogleDriveUploadedFile(
                    "file-1",
                    fileName,
                    "https://drive.google.com/file/d/file-1/view");
            });
        var sut = CreateService(db, driveClient.Object);

        // Act
        var response = await sut.ExportSitesAsync(DefaultQuery(), TestUserId, TestUserEmail, AppRoles.Admin, CancellationToken.None);

        // Assert
        Assert.Equal("file-1", response.FileId);
        Assert.Equal(ExportConstants.SitesFileName, response.FileName);
        Assert.Equal("https://drive.google.com/file/d/file-1/view", response.WebViewLink);
        Assert.Equal(1, response.RowsExported);
        Assert.True(response.WasTruncated);
        Assert.Equal("Google Drive / Redhead Catalog Exports", response.DestinationLabel);
        Assert.Equal(ExportConstants.ExcelContentType, uploadContentType);
        Assert.True(uploadedBytes > 0);
        Assert.DoesNotContain(
            typeof(GoogleDriveExportResponse).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        var connection = await db.GoogleDriveConnections.SingleAsync();
        Assert.Equal("folder-created", connection.ExportFolderId);
        Assert.Equal("Redhead Catalog Exports", connection.ExportFolderName);
    }

    [Fact]
    public async Task ExportSitesAsync_UsesVisibleColumnLogicWhenUploadingWorkbook()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Unlimited);
        SeedConnection(db);
        MemoryStream? uploadedWorkbook = null;
        var driveClient = CreateDriveClientMock();
        driveClient
            .Setup(client => client.UploadFileAsync(
                It.IsAny<string>(),
                "folder-1",
                ExportConstants.SitesFileName,
                It.IsAny<Stream>(),
                ExportConstants.ExcelContentType,
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string, Stream, string, CancellationToken>(async (_, _, fileName, content, _, _) =>
            {
                uploadedWorkbook = new MemoryStream();
                await content.CopyToAsync(uploadedWorkbook);
                uploadedWorkbook.Position = 0;

                return new GoogleDriveUploadedFile("file-1", fileName, null);
            });
        var sut = CreateService(db, driveClient.Object);

        // Act
        await sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            ["traffic", "domain"],
            CancellationToken.None);

        // Assert
        Assert.NotNull(uploadedWorkbook);
        var headers = XlsxTestWorkbook.ReadHeaders(uploadedWorkbook, "Sites");
        Assert.Equal(["Traffic", "Domain"], headers);
    }

    [Fact]
    public async Task ExportSitesAsync_ClientUsageLimitReached_UploadsPartialWorkbookAndLogsGoogleDriveDestination()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(
            db,
            ExportLimitMode.Limited,
            limitRows: 10,
            roleName: AppRoles.Client,
            dailyUniqueDomains: 1,
            weeklyUniqueDomains: 10,
            dailyOperations: 10,
            weeklyOperations: 10);
        SeedConnection(db);
        var driveClient = CreateDriveClientMock();
        var sut = CreateService(db, driveClient.Object);

        // Act
        var response = await sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Client,
            CancellationToken.None);

        // Assert
        Assert.Equal(1, response.RowsExported);
        Assert.True(response.WasTruncated);
        Assert.Equal(ExportConstants.DailyUniqueDomainLimitReached, response.TruncationReason);

        var exportLog = await db.ExportLogs.SingleAsync();
        Assert.Equal(ExportConstants.DestinationGoogleDrive, exportLog.Destination);
        Assert.Equal(ExportConstants.ExportModeSites, exportLog.ExportMode);
        Assert.Equal(1, exportLog.ExportedRowsCount);
        Assert.Single(await db.ExportedDomainAccesses.ToListAsync());
    }

    [Fact]
    public async Task ExportSitesAsync_WhenUploadFails_DoesNotLogSuccessfulExport()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Unlimited);
        SeedConnection(db);
        var driveClient = CreateDriveClientMock();
        driveClient
            .Setup(client => client.UploadFileAsync(
                It.IsAny<string>(),
                "folder-1",
                ExportConstants.SitesFileName,
                It.IsAny<Stream>(),
                ExportConstants.ExcelContentType,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveApiException("Upload failed."));
        var sut = CreateService(db, driveClient.Object);

        // Act
        var act = () => sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<GoogleDriveExportException>(act);
        Assert.Equal(GoogleDriveExportException.UploadFailedErrorCode, exception.ErrorCode);
        Assert.Empty(await db.ExportLogs.ToListAsync());
        Assert.Empty(await db.ExportedDomainAccesses.ToListAsync());
    }

    [Fact]
    public async Task ExportSitesAsync_WhenGoogleRequiresReconnect_MarksConnectionRevoked()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Unlimited);
        SeedConnection(db);
        var driveClient = CreateDriveClientMock();
        driveClient
            .Setup(client => client.RefreshAccessTokenAsync(
                It.IsAny<GoogleDriveOptions>(),
                "refresh-token",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveApiException("Refresh failed.", reconnectRequired: true));
        var sut = CreateService(db, driveClient.Object);

        // Act
        var act = () => sut.ExportSitesAsync(
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<GoogleDriveExportException>(act);
        Assert.Equal(GoogleDriveExportException.ReconnectRequiredErrorCode, ex.ErrorCode);
        var connection = await db.GoogleDriveConnections.SingleAsync();
        Assert.NotNull(connection.RevokedAtUtc);
        Assert.Equal(GoogleDriveExportException.ReconnectRequiredErrorCode, connection.LastError);
    }

    [Fact]
    public async Task ExportMultiSearchAsync_ReusesExportBehaviorAndUploadsWorkbook()
    {
        // Arrange
        await using var db = CreateDbContext();
        SeedExportData(db, ExportLimitMode.Unlimited);
        SeedConnection(db);
        long uploadedBytes = 0;
        var driveClient = CreateDriveClientMock();
        driveClient
            .Setup(client => client.UploadFileAsync(
                It.IsAny<string>(),
                "folder-1",
                ExportConstants.SitesFileName,
                It.IsAny<Stream>(),
                ExportConstants.ExcelContentType,
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string, Stream, string, CancellationToken>(async (_, _, fileName, content, _, _) =>
            {
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy);
                uploadedBytes = copy.Length;

                return new GoogleDriveUploadedFile("file-1", fileName, null);
            });
        var sut = CreateService(db, driveClient.Object);

        // Act
        var response = await sut.ExportMultiSearchAsync(
            "first.com missing.com second.com",
            DefaultQuery(),
            TestUserId,
            TestUserEmail,
            AppRoles.Admin,
            CancellationToken.None);

        // Assert
        Assert.Equal(2, response.RowsExported);
        Assert.False(response.WasTruncated);
        Assert.True(uploadedBytes > 0);

        var exportLog = await db.ExportLogs.SingleAsync();
        Assert.Equal(3, exportLog.RowsReturned);
    }

    private static GoogleDriveExportService CreateService(
        ApplicationDbContext db,
        IGoogleDriveApiClient driveClient)
    {
        var exportService = new ExportService(
            db,
            new SitesQueryBuilder(db),
            new EffectiveExportPolicyService(db),
            new ExportUsageLimitService(db),
            new SitesExcelExportGenerator());

        return new GoogleDriveExportService(
            db,
            exportService,
            driveClient,
            new PlainTextGoogleDriveTokenProtector(),
            Options.Create(new GoogleDriveOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                ExportFolderName = "Redhead Catalog Exports"
            }));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SeedExportData(
        ApplicationDbContext db,
        ExportLimitMode mode,
        int? limitRows = null,
        string roleName = AppRoles.Admin,
        int? dailyUniqueDomains = null,
        int? weeklyUniqueDomains = null,
        int? dailyOperations = null,
        int? weeklyOperations = null)
    {
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = roleName,
            ExportLimitMode = mode,
            ExportLimitRows = limitRows,
            DailyUniqueExportedDomainsLimit = dailyUniqueDomains,
            WeeklyUniqueExportedDomainsLimit = weeklyUniqueDomains,
            DailyExportOperationsLimit = dailyOperations,
            WeeklyExportOperationsLimit = weeklyOperations
        });

        db.Sites.AddRange(
            CreateSite("first.com", 60),
            CreateSite("second.com", 70));

        db.SaveChanges();
    }

    private static void SeedConnection(
        ApplicationDbContext db,
        string? exportFolderId = "folder-1")
    {
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            GoogleEmail = "google@example.com",
            RefreshTokenEncrypted = "refresh-token",
            GrantedScopes = GoogleDriveOptions.DriveFileScope,
            ExportFolderId = exportFolderId,
            ExportFolderName = exportFolderId == null ? null : "Redhead Catalog Exports",
            ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        db.SaveChanges();
    }

    private static SitesQuery DefaultQuery() => new()
    {
        Page = 1,
        PageSize = 100,
        SortBy = SortFields.Domain,
        SortDir = SortingDefaults.Ascending,
        Quarantine = QuarantineFilterValues.All
    };

    private static Site CreateSite(string domain, double dr) => new()
    {
        Domain = domain,
        DR = dr,
        Traffic = 1000,
        Location = "US",
        PriceUsd = 100m,
        PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
        PriceLinkInsertCasinoStatus = ServiceAvailabilityStatus.Unknown,
        PriceDatingStatus = ServiceAvailabilityStatus.Unknown,
        IsQuarantined = false,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private sealed class PlainTextGoogleDriveTokenProtector : IGoogleDriveTokenProtector
    {
        public string ProtectRefreshToken(string refreshToken) => refreshToken;

        public string UnprotectRefreshToken(string protectedRefreshToken) => protectedRefreshToken;
    }

    private static Mock<IGoogleDriveApiClient> CreateDriveClientMock()
    {
        var driveClient = new Mock<IGoogleDriveApiClient>();
        driveClient
            .Setup(client => client.RefreshAccessTokenAsync(
                It.IsAny<GoogleDriveOptions>(),
                "refresh-token",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveAccessToken("access-token"));
        driveClient
            .Setup(client => client.EnsureExportFolderAsync(
                "access-token",
                It.IsAny<string?>(),
                "Redhead Catalog Exports",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string? existingFolderId, string folderName, CancellationToken _) =>
                new GoogleDriveFolder(existingFolderId ?? "folder-created", folderName));
        driveClient
            .Setup(client => client.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string fileName, Stream _, string _, CancellationToken _) =>
                new GoogleDriveUploadedFile("file-1", fileName, null));

        return driveClient;
    }
}
