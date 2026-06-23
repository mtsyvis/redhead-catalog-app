using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ProfileControllerTests
{
    [Fact]
    public async Task GetProfile_ReturnsCurrentUserProfileAndGoogleDriveStatus()
    {
        // Arrange
        var user = CreateUser("Ada", "Lovelace");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var googleDrive = CreateGoogleDriveService(user.Id, connected: true);
        var policyService = CreatePolicyService(
            user,
            AppRoles.Client,
            new EffectiveExportPolicy(
                ExportLimitMode.Limited,
                5000,
                false,
                EffectivePolicySource.Role));
        var usage = new ExportUsageSummary(
            DailyUniqueExportedDomainsUsed: 2,
            DailyUniqueExportedDomainsLimit: 1000,
            WeeklyUniqueExportedDomainsUsed: 4,
            WeeklyUniqueExportedDomainsLimit: 3000,
            DailyExportOperationsUsed: 1,
            DailyExportOperationsLimit: 20,
            WeeklyExportOperationsUsed: 3,
            WeeklyExportOperationsLimit: 60);
        var sut = CreateController(userManager, googleDrive, policyService, usage);

        // Act
        var result = await sut.GetProfile(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal("user@example.com", payload.Email);
        Assert.Equal(AppRoles.Client, payload.Role);
        Assert.Equal("Ada Lovelace", payload.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        Assert.True(payload.GoogleDrive.Connected);
        Assert.Equal(ExportLimitMode.Limited, payload.Limits.ExportLimitMode);
        Assert.Equal(5000, payload.Limits.ExportLimitRows);
        Assert.False(payload.Limits.IsUnlimited);
        Assert.Equal(2, payload.Limits.DailyUniqueExportedDomainsUsed);
        Assert.Equal(1000, payload.Limits.DailyUniqueExportedDomainsLimit);
        Assert.Equal(4, payload.Limits.WeeklyUniqueExportedDomainsUsed);
        Assert.Equal(3000, payload.Limits.WeeklyUniqueExportedDomainsLimit);
        Assert.Equal(1, payload.Limits.DailyExportOperationsUsed);
        Assert.Equal(20, payload.Limits.DailyExportOperationsLimit);
        Assert.Equal(3, payload.Limits.WeeklyExportOperationsUsed);
        Assert.Equal(60, payload.Limits.WeeklyExportOperationsLimit);
        policyService.Verify(
            service => service.GetEffectivePolicyAsync(user, AppRoles.Client, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProfile_ReturnsUnlimitedExportLimit()
    {
        // Arrange
        var user = CreateUser("Root", "User");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.SuperAdmin });
        var googleDrive = CreateGoogleDriveService(user.Id, connected: false);
        var policyService = CreatePolicyService(
            user,
            AppRoles.SuperAdmin,
            new EffectiveExportPolicy(
                ExportLimitMode.Unlimited,
                null,
                false,
                EffectivePolicySource.SuperAdminFixed));
        var sut = CreateController(userManager, googleDrive, policyService);

        // Act
        var result = await sut.GetProfile(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal(ExportLimitMode.Unlimited, payload.Limits.ExportLimitMode);
        Assert.Null(payload.Limits.ExportLimitRows);
        Assert.True(payload.Limits.IsUnlimited);
    }

    [Fact]
    public async Task GetProfile_ReturnsUserOverrideLimitSource()
    {
        // Arrange
        var user = CreateUser("Grace", "Hopper");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var googleDrive = CreateGoogleDriveService(user.Id, connected: false);
        var policyService = CreatePolicyService(
            user,
            AppRoles.Client,
            new EffectiveExportPolicy(
                ExportLimitMode.Limited,
                250,
                true,
                EffectivePolicySource.UserOverride));
        var sut = CreateController(userManager, googleDrive, policyService);

        // Act
        var result = await sut.GetProfile(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal(ExportLimitMode.Limited, payload.Limits.ExportLimitMode);
        Assert.Equal(250, payload.Limits.ExportLimitRows);
        Assert.False(payload.Limits.IsUnlimited);
    }

    [Fact]
    public void CurrentUserProfileResponse_DoesNotExposeSensitiveFields()
    {
        // Arrange
        var sensitiveTerms = new[] { "Password", "Token", "Secret", "Refresh", "SuperAdminNote" };

        // Act
        var propertyNames = typeof(CurrentUserProfileResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(CurrentUserProfileLimitsResponse)
                .GetProperties()
                .Select(property => property.Name));

        // Assert
        Assert.DoesNotContain(propertyNames, property =>
            sensitiveTerms.Any(term => property.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task UpdateProfile_UpdatesOnlyCurrentUserAndTrimsNames()
    {
        // Arrange
        var user = CreateUser(null, null);
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var googleDrive = CreateGoogleDriveService(user.Id, connected: false);
        var policyService = CreatePolicyService(
            user,
            AppRoles.Client,
            new EffectiveExportPolicy(
                ExportLimitMode.Limited,
                5000,
                false,
                EffectivePolicySource.Role));
        var sut = CreateController(userManager, googleDrive, policyService);

        // Act
        var result = await sut.UpdateProfile(
            new UpdateCurrentUserProfileRequest("  Grace Hopper  "),
            CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal("Grace Hopper", user.DisplayName);
        Assert.Equal("Grace Hopper", payload.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        userManager.Verify(manager => manager.UpdateAsync(user), Times.Once);
    }

    private static ProfileController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IGoogleDriveIntegrationService> googleDriveIntegrationService,
        Mock<IEffectiveExportPolicyService> effectiveExportPolicyService,
        ExportUsageSummary? usage = null)
    {
        var usageService = new Mock<IExportUsageLimitService>();
        usageService
            .Setup(service => service.GetUsageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<EffectiveExportPolicy>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage ?? new ExportUsageSummary(
                DailyUniqueExportedDomainsUsed: null,
                DailyUniqueExportedDomainsLimit: null,
                WeeklyUniqueExportedDomainsUsed: null,
                WeeklyUniqueExportedDomainsLimit: null,
                DailyExportOperationsUsed: null,
                DailyExportOperationsLimit: null,
                WeeklyExportOperationsUsed: null,
                WeeklyExportOperationsLimit: null));

        return new(
            userManager.Object,
            googleDriveIntegrationService.Object,
            effectiveExportPolicyService.Object,
            usageService.Object,
            NullLogger<ProfileController>.Instance);
    }

    private static Mock<IEffectiveExportPolicyService> CreatePolicyService(
        ApplicationUser user,
        string role,
        EffectiveExportPolicy policy)
    {
        var policyService = new Mock<IEffectiveExportPolicyService>();
        policyService
            .Setup(service => service.GetEffectivePolicyAsync(user, role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        return policyService;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerForCurrentUser(ApplicationUser user)
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        userManager.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        return userManager;
    }

    private static Mock<IGoogleDriveIntegrationService> CreateGoogleDriveService(
        string userId,
        bool connected)
    {
        var googleDrive = new Mock<IGoogleDriveIntegrationService>();
        googleDrive.Setup(service => service.GetStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveStatusResponse(
                connected,
                connected ? "drive@example.com" : null,
                connected ? DateTime.UtcNow : null,
                connected ? "Exports" : null,
                connected,
                false,
                false));
        return googleDrive;
    }

    private static ApplicationUser CreateUser(string? firstName, string? lastName)
        => new()
        {
            Id = "user-1",
            UserName = "user@example.com",
            Email = "user@example.com",
            DisplayName = string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
                ? null
                : $"{firstName} {lastName}",
            ActivatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
}
