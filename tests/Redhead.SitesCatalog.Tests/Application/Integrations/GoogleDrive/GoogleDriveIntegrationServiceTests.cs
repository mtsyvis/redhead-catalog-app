using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Infrastructure.Options;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

namespace Redhead.SitesCatalog.Tests.Application.Integrations.GoogleDrive;

public sealed class GoogleDriveIntegrationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WhenNoConnectionExists_ReturnsNotConnected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        // Act
        var status = await sut.GetStatusAsync("user-1", CancellationToken.None);

        // Assert
        Assert.False(status.Connected);
        Assert.Null(status.GoogleEmail);
        Assert.Null(status.ConnectedAtUtc);
        Assert.Null(status.ExportFolderName);
        Assert.False(status.HasExportFolderId);
        Assert.False(status.Revoked);
        Assert.False(status.NeedsReconnect);
    }

    [Fact]
    public async Task GetStatusAsync_WhenActiveConnectionExists_ReturnsConnectedWithoutTokenShape()
    {
        // Arrange
        await using var db = CreateDbContext();
        var connectedAt = DateTime.UtcNow.AddMinutes(-5);
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            GoogleEmail = "person@example.com",
            RefreshTokenEncrypted = "protected-token",
            GrantedScopes = GoogleDriveOptions.DriveFileScope,
            ExportFolderId = "folder-1",
            ExportFolderName = "Exports",
            ConnectedAtUtc = connectedAt,
            UpdatedAtUtc = connectedAt
        });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        var status = await sut.GetStatusAsync("user-1", CancellationToken.None);

        // Assert
        Assert.True(status.Connected);
        Assert.Equal("person@example.com", status.GoogleEmail);
        Assert.Equal(connectedAt, status.ConnectedAtUtc);
        Assert.Equal("Exports", status.ExportFolderName);
        Assert.True(status.HasExportFolderId);
        Assert.DoesNotContain(
            typeof(GoogleDriveStatusResponse).GetProperties(),
            property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DisconnectAsync_WhenActiveConnectionExists_SoftRevokesConnection()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            RefreshTokenEncrypted = "protected-token",
            GrantedScopes = GoogleDriveOptions.DriveFileScope,
            ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        await sut.DisconnectAsync("user-1", CancellationToken.None);

        // Assert
        var connection = await db.GoogleDriveConnections.SingleAsync();
        Assert.NotNull(connection.RevokedAtUtc);

        var status = await sut.GetStatusAsync("user-1", CancellationToken.None);
        Assert.False(status.Connected);
        Assert.True(status.Revoked);
        Assert.True(status.NeedsReconnect);
    }

    [Fact]
    public async Task DisconnectAsync_WhenOtherUsersHaveConnections_RevokesOnlyCurrentUserConnection()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.GoogleDriveConnections.AddRange(
            new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                RefreshTokenEncrypted = "protected-token-1",
                GrantedScopes = GoogleDriveOptions.DriveFileScope,
                ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new GoogleDriveConnection
            {
                Id = Guid.NewGuid(),
                UserId = "user-2",
                RefreshTokenEncrypted = "protected-token-2",
                GrantedScopes = GoogleDriveOptions.DriveFileScope,
                ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        // Act
        await sut.DisconnectAsync("user-1", CancellationToken.None);

        // Assert
        var disconnected = await db.GoogleDriveConnections.SingleAsync(connection => connection.UserId == "user-1");
        var otherUser = await db.GoogleDriveConnections.SingleAsync(connection => connection.UserId == "user-2");
        Assert.NotNull(disconnected.RevokedAtUtc);
        Assert.Null(otherUser.RevokedAtUtc);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static GoogleDriveIntegrationService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IGoogleDriveApiClient>(),
            new GoogleDriveOAuthStateService(
                DataProtectionProvider.Create(CreateDataProtectionDirectory()),
                new Microsoft.Extensions.Caching.Memory.MemoryCache(
                    new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new GoogleDriveTokenProtector(DataProtectionProvider.Create(CreateDataProtectionDirectory())),
            Options.Create(new GoogleDriveOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RedirectUri = "https://example.com/api/integrations/google-drive/callback"
            }));

    private static DirectoryInfo CreateDataProtectionDirectory()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "redhead-catalog-tests",
            Guid.NewGuid().ToString()));

}
