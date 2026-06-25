using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Security;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task ListUsers_WithDefaultRequest_ReturnsFirstPageWithDefaultPageSize()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        for (var i = 1; i <= 30; i++)
        {
            await AddUserAsync(db, $"client-{i:00}", $"client-{i:00}@example.com", AppRoles.Client);
        }

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest(), CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(1, payload.Page);
        Assert.Equal(25, payload.PageSize);
        Assert.Equal(30, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Equal(25, payload.Items.Count);
        Assert.Equal("client-01@example.com", payload.Items[0].Email);
        Assert.Equal("client-25@example.com", payload.Items[^1].Email);
    }

    [Fact]
    public async Task ListUsers_IncludesProfileNamesAndCompletionStatus()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            firstName: "Ada",
            lastName: "Lovelace");
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);

        var sut = CreateController(db);

        // Act
        var result = await sut.ListUsers(new UserListRequest { UserType = "all" }, CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        var client = payload.Items.Single(item => item.Id == "client-1");
        Assert.Equal("Ada Lovelace", client.DisplayName);
        Assert.False(client.MustCompleteProfile);

        var existingUserWithoutNames = payload.Items.Single(item => item.Id == "internal-1");
        Assert.Equal("internal@example.com", existingUserWithoutNames.DisplayName);
        Assert.True(existingUserWithoutNames.MustCompleteProfile);
    }

    [Fact]
    public async Task ListUsers_WhenCurrentUserIsSuperAdmin_IncludesSuperAdminNote()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            superAdminNote: "Client owner: Redhead");
        var sut = CreateController(
            db,
            new StubUserManager
            {
                CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
                CurrentRoles = new List<string> { AppRoles.SuperAdmin }
            });

        // Act
        var result = await sut.ListUsers(new UserListRequest { UserType = "all" }, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SuperAdminUserListResponse>(ok.Value);
        var client = payload.Items.Single(item => item.Id == "client-1");
        Assert.Equal("Client owner: Redhead", client.SuperAdminNote);
    }

    [Fact]
    public async Task ListUsers_WhenCurrentUserIsAdmin_DoesNotExposeSuperAdminNote()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            superAdminNote: "Client owner: Redhead");
        var sut = CreateController(
            db,
            new StubUserManager
            {
                CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
                CurrentRoles = new List<string> { AppRoles.Admin }
            });

        // Act
        var result = await sut.ListUsers(new UserListRequest { UserType = "all" }, CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        Assert.Null(typeof(UserListItem).GetProperty("SuperAdminNote"));
        Assert.DoesNotContain("SuperAdminNote", JsonSerializer.Serialize(payload));
    }

    [Fact]
    public async Task GetUser_WhenCurrentUserIsAdmin_ReturnsReadonlyDetailsWithoutSuperAdminNote()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            firstName: "Ada",
            lastName: "Lovelace",
            superAdminNote: "Internal client label");
        var sut = CreateController(
            db,
            new StubUserManager
            {
                CurrentUser = new ApplicationUser { Id = "current-user", Email = "current@example.com" },
                CurrentRoles = new List<string> { AppRoles.Admin }
            });

        // Act
        var result = await sut.GetUser("client-1", CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        Assert.Equal("client-1", payload.Id);
        Assert.Equal("client@example.com", payload.Email);
        Assert.Equal(AppRoles.Client, payload.Role);
        Assert.Equal("Ada Lovelace", payload.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        Assert.True(payload.MustChangePassword);
        Assert.Equal(ExportLimitMode.Limited, payload.EffectiveExportLimitMode);
        Assert.Equal(100, payload.EffectiveExportLimitRows);
        Assert.False(payload.GoogleDriveConnected);
        Assert.False(payload.GoogleDrive.Connected);
        Assert.DoesNotContain("SuperAdminNote", JsonSerializer.Serialize(payload));
    }

    [Fact]
    public async Task GetUser_WhenCurrentUserIsSuperAdmin_IncludesSuperAdminNote()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            superAdminNote: "Client owner: Redhead");
        var sut = CreateController(
            db,
            new StubUserManager
            {
                CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
                CurrentRoles = new List<string> { AppRoles.SuperAdmin }
            });

        // Act
        var result = await sut.GetUser("client-1", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SuperAdminUserDetailsResponse>(ok.Value);
        Assert.Equal("Client owner: Redhead", payload.SuperAdminNote);
    }

    [Fact]
    public async Task GetUser_WhenProfileIsIncomplete_ReturnsEmailDisplayNameAndMustCompleteProfile()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(
            db,
            "client-1",
            "client@example.com",
            AppRoles.Client,
            firstName: "Ada",
            lastName: " ");
        var sut = CreateController(db);

        // Act
        var result = await sut.GetUser("client-1", CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        Assert.Equal("client@example.com", payload.DisplayName);
        Assert.True(payload.MustCompleteProfile);
    }

    [Fact]
    public async Task GetUser_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        var sut = CreateController(db);

        // Act
        var result = await sut.GetUser("missing-user", CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(notFound.Value);
        Assert.Equal("User not found.", payload.Message);
    }

    [Fact]
    public async Task GetUser_WhenGoogleDriveConnectionIsActive_ReturnsGoogleDriveStatusWithoutTokenFields()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);
        db.GoogleDriveConnections.Add(new GoogleDriveConnection
        {
            Id = Guid.NewGuid(),
            UserId = "client-1",
            GoogleEmail = "drive-user@example.com",
            RefreshTokenEncrypted = "encrypted-refresh-token",
            GrantedScopes = GoogleDriveOptions.DriveFileScope,
            ExportFolderId = "folder-1",
            ExportFolderName = "Redhead Catalog Exports",
            ConnectedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 20, 12, 5, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        var sut = CreateController(db);

        // Act
        var result = await sut.GetUser("client-1", CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        Assert.True(payload.GoogleDriveConnected);
        Assert.True(payload.GoogleDrive.Connected);
        Assert.Equal("drive-user@example.com", payload.GoogleDrive.GoogleEmail);
        Assert.Equal("Redhead Catalog Exports", payload.GoogleDrive.ExportFolderName);
        Assert.True(payload.GoogleDrive.HasExportFolderId);
        Assert.DoesNotContain(
            typeof(GoogleDriveStatusResponse).GetProperties().Select(property => property.Name),
            name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetUser_WhenClientHasRecentExportUsage_ReturnsCurrentClientUsage()
    {
        // Arrange
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);
        var nowUtc = DateTime.UtcNow;
        var successful = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = "client-1",
            UserEmail = "client@example.com",
            Role = AppRoles.Client,
            TimestampUtc = nowUtc.AddHours(-1),
            RequestedRowsCount = 2,
            ExportedRowsCount = 2,
            RowsReturned = 2,
            Destination = ExportConstants.DestinationDownload,
            ExportMode = ExportConstants.ExportModeSites
        };
        var partial = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = "client-1",
            UserEmail = "client@example.com",
            Role = AppRoles.Client,
            TimestampUtc = nowUtc.AddHours(-2),
            RequestedRowsCount = 2,
            ExportedRowsCount = 1,
            RowsReturned = 1,
            WasTruncated = true,
            ExportLimitRows = 1,
            Destination = ExportConstants.DestinationDownload,
            ExportMode = ExportConstants.ExportModeSites
        };
        var weekly = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = "client-1",
            UserEmail = "client@example.com",
            Role = AppRoles.Client,
            TimestampUtc = nowUtc.AddDays(-3),
            RequestedRowsCount = 1,
            ExportedRowsCount = 1,
            RowsReturned = 1,
            Destination = ExportConstants.DestinationDownload,
            ExportMode = ExportConstants.ExportModeSites
        };
        var blocked = new ExportLog
        {
            Id = Guid.NewGuid(),
            UserId = "client-1",
            UserEmail = "client@example.com",
            Role = AppRoles.Client,
            TimestampUtc = nowUtc.AddHours(-4),
            RequestedRowsCount = 1,
            ExportedRowsCount = 0,
            RowsReturned = 0,
            Destination = ExportConstants.DestinationDownload,
            ExportMode = ExportConstants.ExportModeSites,
            BlockedReason = ExportConstants.DailyExportOperationLimitReached
        };
        db.ExportLogs.AddRange(successful, partial, weekly, blocked);
        db.ExportedDomainAccesses.AddRange(
            new ExportedDomainAccess
            {
                Id = Guid.NewGuid(),
                ExportLogId = successful.Id,
                ExportLog = successful,
                UserId = "client-1",
                Domain = "alpha.com",
                ExportedAtUtc = successful.TimestampUtc
            },
            new ExportedDomainAccess
            {
                Id = Guid.NewGuid(),
                ExportLogId = partial.Id,
                ExportLog = partial,
                UserId = "client-1",
                Domain = "beta.com",
                ExportedAtUtc = partial.TimestampUtc
            },
            new ExportedDomainAccess
            {
                Id = Guid.NewGuid(),
                ExportLogId = partial.Id,
                ExportLog = partial,
                UserId = "client-1",
                Domain = "alpha.com",
                ExportedAtUtc = partial.TimestampUtc
            },
            new ExportedDomainAccess
            {
                Id = Guid.NewGuid(),
                ExportLogId = weekly.Id,
                ExportLog = weekly,
                UserId = "client-1",
                Domain = "gamma.com",
                ExportedAtUtc = weekly.TimestampUtc
            });
        await db.SaveChangesAsync();
        var sut = CreateController(db);

        // Act
        var result = await sut.GetUser("client-1", CancellationToken.None);

        // Assert
        var payload = GetOkPayload(result);
        Assert.NotNull(payload.ClientExportUsage);
        Assert.Equal(2, payload.ClientExportUsage.DailyUniqueExportedDomainsUsed);
        Assert.Equal(1000, payload.ClientExportUsage.DailyUniqueExportedDomainsLimit);
        Assert.Equal(3, payload.ClientExportUsage.WeeklyUniqueExportedDomainsUsed);
        Assert.Equal(3000, payload.ClientExportUsage.WeeklyUniqueExportedDomainsLimit);
        Assert.Equal(2, payload.ClientExportUsage.DailyExportOperationsUsed);
        Assert.Equal(20, payload.ClientExportUsage.DailyExportOperationsLimit);
        Assert.Equal(3, payload.ClientExportUsage.WeeklyExportOperationsUsed);
        Assert.Equal(60, payload.ClientExportUsage.WeeklyExportOperationsLimit);
    }

    [Fact]
    public void GetUser_UsesUsersReadAuthorizationPolicy()
    {
        // Arrange
        var controllerType = typeof(AdminUsersController);
        var method = controllerType.GetMethod(nameof(AdminUsersController.GetUser));

        // Act
        var controllerPolicies = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToList();
        var allowsAnonymous = method?
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Any() == true;

        // Assert
        Assert.Contains(AppPolicies.UsersReadAccess, controllerPolicies);
        Assert.False(allowsAnonymous);
    }

    [Fact]
    public void AdminUserDetailsResponse_DoesNotExposeSensitiveAuthOrGoogleTokenFields()
    {
        // Arrange
        var sensitiveTerms = new[]
        {
            "PasswordHash",
            "SecurityStamp",
            "ConcurrencyStamp",
            "AccessToken",
            "RefreshToken",
            "RefreshTokenEncrypted",
            "Token",
            "SuperAdminNote"
        };

        // Act
        var exposedPropertyNames = typeof(AdminUserDetailsResponse)
            .GetProperties()
            .Concat(typeof(GoogleDriveStatusResponse).GetProperties())
            .Select(property => property.Name)
            .ToList();

        // Assert
        foreach (var term in sensitiveTerms)
        {
            Assert.DoesNotContain(
                exposedPropertyNames,
                propertyName => propertyName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task ListUsers_WithAllFilter_ReturnsAllUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);
        await AddUserAsync(db, "lite-1", "lite@example.com", AppRoles.Lite);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "all" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(5, payload.TotalCount);
        Assert.Equal(
            [AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Internal, AppRoles.Client, AppRoles.Lite],
            payload.Items.Select(item => item.Role));
    }

    [Fact]
    public async Task ListUsers_WithClientsFilter_ReturnsClientAndLiteUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "client-1", "client-a@example.com", AppRoles.Client);
        await AddUserAsync(db, "lite-1", "lite@example.com", AppRoles.Lite);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "clients" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal([AppRoles.Client, AppRoles.Lite], payload.Items.Select(item => item.Role));
    }

    [Fact]
    public async Task ListUsers_WithInternalFilter_ReturnsNonClientUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);
        await AddUserAsync(db, "future-1", "future-role@example.com", "FutureInternal");
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);
        await AddUserAsync(db, "lite-1", "lite@example.com", AppRoles.Lite);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "internal" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(4, payload.TotalCount);
        Assert.DoesNotContain(payload.Items, item => item.Role == AppRoles.Client);
        Assert.DoesNotContain(payload.Items, item => item.Role == AppRoles.Lite);
        Assert.Contains(payload.Items, item => item.Role == "FutureInternal");
    }

    [Fact]
    public async Task ListUsers_WithPagination_ReturnsCorrectItemsAndCounts()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        for (var i = 1; i <= 12; i++)
        {
            await AddUserAsync(db, $"client-{i:00}", $"client-{i:00}@example.com", AppRoles.Client);
        }

        var sut = CreateController(db);

        var result = await sut.ListUsers(
            new UserListRequest { UserType = "clients", Page = 2, PageSize = 10 },
            CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(2, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(12, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Equal(["client-11@example.com", "client-12@example.com"], payload.Items.Select(item => item.Email));
    }

    [Theory]
    [InlineData("unknown", 1, 25, "Invalid userType")]
    [InlineData("", 1, 25, "Invalid userType")]
    [InlineData("all", 0, 25, "Page must be greater than or equal to 1.")]
    [InlineData("all", 1, 15, "Invalid pageSize")]
    public async Task ListUsers_WithInvalidQuery_ReturnsBadRequest(
        string userType,
        int page,
        int pageSize,
        string expectedMessage)
    {
        await using var db = CreateDbContext();
        var sut = CreateController(db);

        var result = await sut.ListUsers(
            new UserListRequest { UserType = userType, Page = page, PageSize = pageSize },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains(expectedMessage, payload.Message);
    }

    [Fact]
    public async Task ListUsers_OrdersBeforePaginationUsingStableOrdering()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "client-2", "same@example.com", AppRoles.Client);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "client-1", "same@example.com", AppRoles.Client);
        await AddUserAsync(db, "disabled-superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin, isActive: false);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { Page = 1, PageSize = 10 }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(
            ["admin-1", "internal-1", "client-1", "client-2", "disabled-superadmin-1"],
            payload.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserIsAdmin_ReturnsForbid()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest(
            "new-user@example.com",
            AppRoles.Client,
            "Should not save"));

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        Assert.Null(userManager.CreatedUser);
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserIsSuperAdmin_ReturnsOk()
    {
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        var result = await sut.CreateUser(new CreateUserRequest("new-user@example.com", AppRoles.Client));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CreateUserResponse>(ok.Value);
        Assert.Equal("new-user@example.com", payload.Email);
        Assert.Equal(AppRoles.Client, payload.Role);
        Assert.False(string.IsNullOrWhiteSpace(payload.ActivationPath));
        Assert.Null(userManager.CreatedUser?.DisplayName);
        Assert.Null(userManager.CreatedUser?.ActivatedAtUtc);
        Assert.NotNull(userManager.CreatedUser?.InvitationTokenHash);
        var token = Uri.UnescapeDataString(payload.ActivationPath.Split("token=", 2)[1]);
        Assert.Equal(UserInvitationToken.Hash(token), userManager.CreatedUser?.InvitationTokenHash);
        Assert.InRange(
            payload.InvitationExpiresAtUtc,
            DateTime.UtcNow.AddHours(71),
            DateTime.UtcNow.AddHours(73));
    }

    [Fact]
    public async Task CreateUser_WhenRoleIsLite_ReturnsOk()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest("lite@example.com", AppRoles.Lite));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CreateUserResponse>(ok.Value);
        Assert.Equal(AppRoles.Lite, payload.Role);
        Assert.Equal(AppRoles.Lite, userManager.AddedRole);
    }

    [Fact]
    public async Task ReissueInvitation_WhenUserIsPending_ReplacesTokenAndExpiry()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "pending-user",
            Email = "pending@example.com",
            IsActive = true,
            InvitationTokenHash = UserInvitationToken.Hash("old-token"),
            InvitationExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReissueInvitation(targetUser.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ReissueInvitationResponse>(ok.Value);
        var token = Uri.UnescapeDataString(payload.ActivationPath.Split("token=", 2)[1]);
        Assert.Equal(UserInvitationToken.Hash(token), targetUser.InvitationTokenHash);
        Assert.NotEqual(UserInvitationToken.Hash("old-token"), targetUser.InvitationTokenHash);
        Assert.Equal(payload.InvitationExpiresAtUtc, targetUser.InvitationExpiresAtUtc);
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserIsSuperAdmin_TrimsAndSavesSuperAdminNote()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest(
            "new-user@example.com",
            AppRoles.Client,
            "  Client owner: Redhead  "));

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Client owner: Redhead", userManager.CreatedUser?.SuperAdminNote);
    }

    [Fact]
    public async Task CreateUser_WhenSuperAdminNoteIsWhitespace_SavesNull()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest(
            "new-user@example.com",
            AppRoles.Client,
            "   "));

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(userManager.CreatedUser?.SuperAdminNote);
    }

    [Fact]
    public async Task CreateUser_WhenSuperAdminNoteIsTooLong_ReturnsBadRequest()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest(
            "new-user@example.com",
            AppRoles.Client,
            new string('a', 1001)));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("1000 characters or fewer", payload.Message);
        Assert.Null(userManager.CreatedUser);
    }

    [Fact]
    public async Task CreateUser_WhenEmailBelongsToDisabledUser_ReturnsReactivationGuidance()
    {
        // Arrange
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            ExistingUserByEmail = new ApplicationUser
            {
                Id = "disabled-client-1",
                Email = "client@example.com",
                IsActive = false
            }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.CreateUser(new CreateUserRequest("client@example.com", AppRoles.Client));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("Reactivate", payload.Message);
        Assert.Null(userManager.CreatedUser);
    }

    [Fact]
    public async Task UpdateUserRole_WhenSuperAdminChangesNormalUserRole_UpdatesRoleAndPreservesOverride()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            IsActive = true,
            ExportLimitOverrideMode = ExportLimitMode.Limited,
            ExportLimitRowsOverride = 250
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserRole(
            targetUser.Id,
            new UpdateUserRoleRequest(AppRoles.Internal));

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal([AppRoles.Client], userManager.RemovedRoles);
        Assert.Equal(AppRoles.Internal, userManager.AddedRole);
        Assert.Equal(ExportLimitMode.Limited, targetUser.ExportLimitOverrideMode);
        Assert.Equal(250, targetUser.ExportLimitRowsOverride);
        Assert.Equal(1, userManager.SecurityStampUpdateCount);
    }

    [Fact]
    public async Task UpdateUserRole_WhenRequestedRoleIsLite_UpdatesRole()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "client-1", Email = "client@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserRole(
            targetUser.Id,
            new UpdateUserRoleRequest(AppRoles.Lite));

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal([AppRoles.Client], userManager.RemovedRoles);
        Assert.Equal(AppRoles.Lite, userManager.AddedRole);
        Assert.Equal(1, userManager.SecurityStampUpdateCount);
    }

    [Fact]
    public async Task UpdateUserRole_WhenCurrentUserIsAdmin_ReturnsForbid()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "client-1", Email = "client@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserRole(
            targetUser.Id,
            new UpdateUserRoleRequest(AppRoles.Internal));

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Null(userManager.AddedRole);
        Assert.Equal(0, userManager.SecurityStampUpdateCount);
    }

    [Fact]
    public async Task UpdateUserRole_WhenChangingOwnRole_ReturnsBadRequest()
    {
        // Arrange
        var currentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = currentUser,
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = currentUser,
            TargetRoles = new List<string> { AppRoles.Admin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserRole(
            currentUser.Id,
            new UpdateUserRoleRequest(AppRoles.Internal));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("own role", payload.Message);
    }

    [Theory]
    [InlineData(AppRoles.SuperAdmin, AppRoles.Admin)]
    [InlineData(AppRoles.Client, AppRoles.SuperAdmin)]
    public async Task UpdateUserRole_WhenRoleChangeInvolvesSuperAdmin_ReturnsBadRequest(
        string currentRole,
        string requestedRole)
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "target-1", Email = "target@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { currentRole }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserRole(
            targetUser.Id,
            new UpdateUserRoleRequest(requestedRole));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("protected role", payload.Message);
        Assert.Null(userManager.AddedRole);
    }

    [Fact]
    public async Task DisableUser_WhenDisablingOwnAccount_ReturnsBadRequest()
    {
        // Arrange
        var currentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = currentUser,
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = currentUser,
            TargetRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.DisableUser(currentUser.Id);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("own account", payload.Message);
        Assert.True(currentUser.IsActive);
    }

    [Fact]
    public async Task DisableUser_WhenTargetIsLastActiveSuperAdmin_ReturnsBadRequest()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "superadmin-2", Email = "target@example.com", IsActive = true };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "current@example.com", IsActive = true },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.SuperAdmin },
            SuperAdminUsers = new List<ApplicationUser> { targetUser }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.DisableUser(targetUser.Id);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("last active SuperAdmin", payload.Message);
        Assert.True(targetUser.IsActive);
    }

    [Fact]
    public async Task ReactivateUser_WhenNormalUserIsDisabled_ActivatesWithSelectedRoleAndTemporaryPassword()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            IsActive = false,
            MustChangePassword = false,
            DisplayName = "Ada Lovelace",
            ActivatedAtUtc = DateTime.UtcNow,
            SuperAdminNote = "Preserve note",
            ExportLimitOverrideMode = ExportLimitMode.Limited,
            ExportLimitRowsOverride = 300
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client },
            ExistingUserByEmail = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.Internal));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ReactivateUserResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.TemporaryPassword));
        Assert.True(targetUser.IsActive);
        Assert.True(targetUser.MustChangePassword);
        Assert.Equal("Ada Lovelace", targetUser.DisplayName);
        Assert.Equal("Preserve note", targetUser.SuperAdminNote);
        Assert.Equal([AppRoles.Client], userManager.RemovedRoles);
        Assert.Equal(AppRoles.Internal, userManager.AddedRole);
        Assert.Equal(1, userManager.ResetPasswordCount);
        Assert.Equal(1, userManager.SecurityStampUpdateCount);
    }

    [Fact]
    public async Task ReactivateUser_WhenRequestedRoleIsLite_ActivatesWithLiteRole()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            IsActive = false,
            ActivatedAtUtc = DateTime.UtcNow
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client },
            ExistingUserByEmail = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.Lite));

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(targetUser.IsActive);
        Assert.Equal([AppRoles.Client], userManager.RemovedRoles);
        Assert.Equal(AppRoles.Lite, userManager.AddedRole);
        Assert.Equal(1, userManager.SecurityStampUpdateCount);
    }

    [Fact]
    public async Task ReactivateUser_WhenCurrentUserIsAdmin_ReturnsForbid()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "client-1", Email = "client@example.com", IsActive = false };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.Internal));

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        Assert.False(targetUser.IsActive);
        Assert.Equal(0, userManager.ResetPasswordCount);
    }

    [Fact]
    public async Task ReactivateUser_WhenDisabledSuperAdminIsReactivatedAsSuperAdmin_ReturnsTemporaryPassword()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "superadmin-2",
            Email = "target@example.com",
            IsActive = false,
            MustChangePassword = false,
            DisplayName = "Second SuperAdmin",
            ActivatedAtUtc = DateTime.UtcNow
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.SuperAdmin },
            ExistingUserByEmail = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.SuperAdmin));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ReactivateUserResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.TemporaryPassword));
        Assert.True(targetUser.IsActive);
        Assert.True(targetUser.MustChangePassword);
        Assert.Null(userManager.AddedRole);
        Assert.Equal(1, userManager.ResetPasswordCount);
    }

    [Fact]
    public async Task ReactivateUser_WhenDisabledSuperAdminIsReactivatedAsNormalRole_ReturnsBadRequest()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "superadmin-2", Email = "target@example.com", IsActive = false };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.Admin));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("Invalid reactivation role", payload.Message);
        Assert.False(targetUser.IsActive);
    }

    [Fact]
    public async Task ReactivateUser_WhenEmailIsUsedByAnotherActiveUser_ReturnsBadRequest()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = "client-1", Email = "client@example.com", IsActive = false };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser,
            TargetRoles = new List<string> { AppRoles.Client },
            ExistingUserByEmail = new ApplicationUser
            {
                Id = "client-2",
                Email = "client@example.com",
                IsActive = true
            }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.ReactivateUser(
            targetUser.Id,
            new ReactivateUserRequest(AppRoles.Internal));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("Another active user", payload.Message);
        Assert.False(targetUser.IsActive);
        Assert.Equal(0, userManager.ResetPasswordCount);
    }

    [Fact]
    public async Task UpdateUserSuperAdminNote_WhenCurrentUserIsSuperAdmin_UpdatesTrimmedNote()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            SuperAdminNote = "Old note"
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserSuperAdminNote(
            targetUser.Id,
            new UpdateUserSuperAdminNoteRequest("  Updated note  "),
            CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Updated note", targetUser.SuperAdminNote);
    }

    [Fact]
    public async Task UpdateUserSuperAdminNote_WhenCurrentUserIsAdmin_ReturnsForbidAndDoesNotUpdate()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            SuperAdminNote = "Old note"
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin },
            TargetUserById = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserSuperAdminNote(
            targetUser.Id,
            new UpdateUserSuperAdminNoteRequest("Updated note"),
            CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Old note", targetUser.SuperAdminNote);
    }

    [Fact]
    public async Task UpdateUserSuperAdminNote_WhenSuperAdminNoteIsWhitespace_SavesNull()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            SuperAdminNote = "Old note"
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserSuperAdminNote(
            targetUser.Id,
            new UpdateUserSuperAdminNoteRequest("   "),
            CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Null(targetUser.SuperAdminNote);
    }

    [Fact]
    public async Task UpdateUserSuperAdminNote_WhenSuperAdminNoteIsTooLong_ReturnsBadRequest()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            SuperAdminNote = "Old note"
        };
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin },
            TargetUserById = targetUser
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserSuperAdminNote(
            targetUser.Id,
            new UpdateUserSuperAdminNoteRequest(new string('a', 1001)),
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("1000 characters or fewer", payload.Message);
        Assert.Equal("Old note", targetUser.SuperAdminNote);
    }

    [Fact]
    public async Task UpdateUserExportLimit_DoesNotChangeTargetUserProfileNames()
    {
        // Arrange
        var targetUser = new ApplicationUser
        {
            Id = "client-1",
            Email = "client@example.com",
            DisplayName = "Ada Lovelace",
            ActivatedAtUtc = DateTime.UtcNow,
            ExportLimitOverrideMode = null,
            ExportLimitRowsOverride = null
        };
        var userManager = new StubUserManager
        {
            TargetUserById = targetUser,
            CurrentRoles = new List<string> { AppRoles.Client }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

        // Act
        var result = await sut.UpdateUserExportLimit(
            targetUser.Id,
            new UpdateUserExportLimitRequest(ExportLimitMode.Limited, 500),
            CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Ada Lovelace", targetUser.DisplayName);
        Assert.Equal(ExportLimitMode.Limited, targetUser.ExportLimitOverrideMode);
        Assert.Equal(500, targetUser.ExportLimitRowsOverride);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AdminUsersController CreateController(ApplicationDbContext db)
    {
        return CreateController(db, new StubUserManager());
    }

    private static AdminUsersController CreateController(
        ApplicationDbContext db,
        StubUserManager userManager)
    {
        return new AdminUsersController(
            userManager,
            new AdminUsersListService(
                db,
                CreateGoogleDriveIntegrationService(db),
                new ExportUsageLimitService(db)),
            NullLogger<AdminUsersController>.Instance);
    }

    private static UserListResponse GetOkPayload(ActionResult<UserListResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<UserListResponse>(ok.Value);
    }

    private static AdminUserDetailsResponse GetOkPayload(ActionResult<AdminUserDetailsResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AdminUserDetailsResponse>(ok.Value);
    }

    private static GoogleDriveIntegrationService CreateGoogleDriveIntegrationService(ApplicationDbContext db)
    {
        return new GoogleDriveIntegrationService(
            db,
            Mock.Of<IGoogleDriveApiClient>(),
            Mock.Of<IGoogleDriveOAuthStateService>(),
            Mock.Of<IGoogleDriveTokenProtector>(),
            Options.Create(new GoogleDriveOptions()));
    }

    private static async Task SeedRoleSettingsAsync(ApplicationDbContext db)
    {
        db.RoleSettings.AddRange(
            new RoleSettings { RoleName = AppRoles.SuperAdmin, ExportLimitMode = ExportLimitMode.Unlimited },
            new RoleSettings { RoleName = AppRoles.Admin, ExportLimitMode = ExportLimitMode.Unlimited },
            new RoleSettings { RoleName = AppRoles.Internal, ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 1000 },
            new RoleSettings
            {
                RoleName = AppRoles.Client,
                ExportLimitMode = ExportLimitMode.Limited,
                ExportLimitRows = 100,
                DailyUniqueExportedDomainsLimit = 1000,
                WeeklyUniqueExportedDomainsLimit = 3000,
                DailyExportOperationsLimit = 20,
                WeeklyExportOperationsLimit = 60
            },
            new RoleSettings
            {
                RoleName = AppRoles.Lite,
                ExportLimitMode = ExportLimitMode.Disabled
            });

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> AddUserAsync(
        ApplicationDbContext db,
        string id,
        string email,
        string roleName,
        bool isActive = true,
        string? firstName = null,
        string? lastName = null,
        string? superAdminNote = null)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
        if (role == null)
        {
            role = new IdentityRole
            {
                Id = $"role-{roleName}",
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };
            db.Roles.Add(role);
        }

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = isActive,
            DisplayName = string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
                ? null
                : $"{firstName} {lastName}",
            ActivatedAtUtc = DateTime.UtcNow,
            SuperAdminNote = superAdminNote
        };
        db.Users.Add(user);

        db.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = id,
            RoleId = role.Id
        });

        await db.SaveChangesAsync();
        return user;
    }

    private sealed class StubUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUser? CurrentUser { get; init; }
        public IList<string> CurrentRoles { get; init; } = new List<string>();
        public IList<string> TargetRoles { get; init; } = new List<string>();
        public IList<ApplicationUser> SuperAdminUsers { get; init; } = new List<ApplicationUser>();
        public ApplicationUser? ExistingUserByEmail { get; set; }
        public ApplicationUser? TargetUserById { get; init; }
        public ApplicationUser? CreatedUser { get; private set; }
        public List<string> RemovedRoles { get; } = [];
        public string? AddedRole { get; private set; }
        public int SecurityStampUpdateCount { get; private set; }
        public int ResetPasswordCount { get; private set; }

        public StubUserManager()
            : base(
                new StubUserStore(),
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!)
        {
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
            => Task.FromResult(CurrentUser);

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            if (CurrentUser != null && user.Id == CurrentUser.Id)
            {
                return Task.FromResult(CurrentRoles);
            }

            if (TargetUserById != null && user.Id == TargetUserById.Id)
            {
                return Task.FromResult(TargetRoles);
            }

            return Task.FromResult<IList<string>>(new List<string>());
        }

        public override Task<ApplicationUser?> FindByEmailAsync(string email)
            => Task.FromResult(ExistingUserByEmail);

        public override Task<ApplicationUser?> FindByIdAsync(string userId)
            => Task.FromResult(TargetUserById?.Id == userId ? TargetUserById : null);

        public override Task<IdentityResult> CreateAsync(ApplicationUser user)
        {
            user.Id = "created-user-1";
            CreatedUser = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
        {
            AddedRole = role;
            if (TargetUserById != null && user.Id == TargetUserById.Id)
            {
                TargetRoles.Add(role);
            }

            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IdentityResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            var roleList = roles.ToList();
            RemovedRoles.AddRange(roleList);
            foreach (var role in roleList)
            {
                TargetRoles.Remove(role);
            }

            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IdentityResult> UpdateAsync(ApplicationUser user)
            => Task.FromResult(IdentityResult.Success);

        public override Task<IdentityResult> UpdateSecurityStampAsync(ApplicationUser user)
        {
            SecurityStampUpdateCount++;
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
            => Task.FromResult("reset-token");

        public override Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
        {
            ResetPasswordCount++;
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName)
        {
            IList<ApplicationUser> users = string.Equals(roleName, AppRoles.SuperAdmin, StringComparison.Ordinal)
                ? SuperAdminUsers
                : new List<ApplicationUser>();
            return Task.FromResult(users);
        }
    }

    private sealed class StubUserStore : IUserStore<ApplicationUser>
    {
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public void Dispose()
        {
        }
    }
}
