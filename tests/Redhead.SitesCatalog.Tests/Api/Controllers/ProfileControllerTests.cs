using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

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
            .ReturnsAsync(new List<string> { AppRoles.Internal });
        var googleDrive = CreateGoogleDriveService(user.Id, connected: true);
        var sut = CreateController(userManager, googleDrive);

        // Act
        var result = await sut.GetProfile(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal("user@example.com", payload.Email);
        Assert.Equal(AppRoles.Internal, payload.Role);
        Assert.Equal("Ada Lovelace", payload.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        Assert.True(payload.GoogleDrive.Connected);
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
        var sut = CreateController(userManager, googleDrive);

        // Act
        var result = await sut.UpdateProfile(
            new UpdateCurrentUserProfileRequest("  Grace  ", "  Hopper  "),
            CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CurrentUserProfileResponse>(ok.Value);
        Assert.Equal("Grace", user.FirstName);
        Assert.Equal("Hopper", user.LastName);
        Assert.Equal("Grace Hopper", payload.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        userManager.Verify(manager => manager.UpdateAsync(user), Times.Once);
    }

    private static ProfileController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IGoogleDriveIntegrationService> googleDriveIntegrationService)
        => new(
            userManager.Object,
            googleDriveIntegrationService.Object,
            NullLogger<ProfileController>.Instance);

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
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };
}
