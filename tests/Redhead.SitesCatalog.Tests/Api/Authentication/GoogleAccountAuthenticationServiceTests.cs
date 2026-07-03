using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Redhead.SitesCatalog.Api.Authentication;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Authentication;

public sealed class GoogleAccountAuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WhenIdentityIsNew_CreatesGoogleOnlyLiteUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var userManager = CreateUserManager();
        ApplicationUser? createdUser = null;
        string? assignedRole = null;
        UserLoginInfo? addedLogin = null;
        Claim? addedAvatarClaim = null;
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.FindByEmailAsync("person@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(user =>
            {
                user.Id = "google-user-1";
                createdUser = user;
            })
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((_, role) => assignedRole = role)
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
            .Callback<ApplicationUser, UserLoginInfo>((_, login) => addedLogin = login)
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetClaimsAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<Claim>());
        userManager.Setup(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()))
            .Callback<ApplicationUser, Claim>((_, claim) => addedAvatarClaim = claim)
            .ReturnsAsync(IdentityResult.Success);
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "person@example.com",
            true,
            "  Ada Lovelace  ",
            "https://lh3.googleusercontent.com/avatar"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.Success, result.Status);
        Assert.True(result.Created);
        Assert.Same(createdUser, result.User);
        Assert.Equal(AppRoles.Lite, assignedRole);
        Assert.NotNull(createdUser);
        Assert.True(createdUser.EmailConfirmed);
        Assert.True(createdUser.IsActive);
        Assert.False(createdUser.MustChangePassword);
        Assert.Equal("Ada Lovelace", createdUser.DisplayName);
        Assert.NotNull(createdUser.ActivatedAtUtc);
        Assert.Null(createdUser.PasswordHash);
        Assert.Equal(ExternalLoginProviders.Google, addedLogin?.LoginProvider);
        Assert.Equal("subject-1", addedLogin?.ProviderKey);
        Assert.Equal(AppClaimTypes.GoogleAvatarUrl, addedAvatarClaim?.Type);
        Assert.Equal("https://lh3.googleusercontent.com/avatar", addedAvatarClaim?.Value);
        Assert.Empty(db.GoogleDriveConnections);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenGoogleSubjectIsAlreadyLinked_ReturnsExistingUserWithoutProvisioning()
    {
        // Arrange
        await using var db = CreateDbContext();

        var linkedUser = new ApplicationUser { Id = "existing", IsActive = true };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync(linkedUser);
        userManager.Setup(manager => manager.GetRolesAsync(linkedUser))
            .ReturnsAsync(new List<string> { AppRoles.Admin });
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "changed@example.com",
            true,
            "Ada Lovelace"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.Success, result.Status);
        Assert.False(result.Created);
        Assert.Same(linkedUser, result.User);
        userManager.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        userManager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenLinkedAccountHasNoRole_RejectsIncompleteAccount()
    {
        // Arrange
        await using var db = CreateDbContext();
        var linkedUser = new ApplicationUser { Id = "incomplete", IsActive = true };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync(linkedUser);
        userManager.Setup(manager => manager.GetRolesAsync(linkedUser))
            .ReturnsAsync(new List<string>());
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "person@example.com",
            true,
            "Ada Lovelace"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.ProvisioningFailed, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenEmailAlreadyExists_DoesNotAutoLinkOrChangeUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var existingUser = new ApplicationUser { Id = "privileged", IsActive = true };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.FindByEmailAsync("admin@example.com"))
            .ReturnsAsync(existingUser);
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "admin@example.com",
            true,
            "Existing Admin"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.EmailConflict, result.Status);
        Assert.Null(result.User);
        userManager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        userManager.Verify(
            manager => manager.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenEmailIsNotVerified_RejectsIdentityBeforeLookup()
    {
        // Arrange
        await using var db = CreateDbContext();
        var userManager = CreateUserManager();
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "person@example.com",
            false,
            "Ada Lovelace"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.InvalidIdentity, result.Status);
        userManager.Verify(
            manager => manager.FindByLoginAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenLinkedUserIsDisabled_DoesNotSignInOrCreateReplacement()
    {
        // Arrange
        await using var db = CreateDbContext();
        var linkedUser = new ApplicationUser { Id = "disabled", IsActive = false };
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync(linkedUser);
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "person@example.com",
            true,
            "Ada Lovelace"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.Disabled, result.Status);
        Assert.Null(result.User);
        userManager.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenLiteRoleAssignmentFails_DoesNotAddExternalLogin()
    {
        // Arrange
        await using var db = CreateDbContext();
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByLoginAsync(ExternalLoginProviders.Google, "subject-1"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.FindByEmailAsync("person@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Lite))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "RoleFailure" }));
        var sut = CreateService(db, userManager);

        // Act
        var result = await sut.AuthenticateAsync(new GoogleIdentity(
            "subject-1",
            "person@example.com",
            true,
            "Ada Lovelace"));

        // Assert
        Assert.Equal(GoogleAccountAuthenticationStatus.ProvisioningFailed, result.Status);
        userManager.Verify(
            manager => manager.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()),
            Times.Never);
    }

    private static GoogleAccountAuthenticationService CreateService(
        ApplicationDbContext db,
        Mock<UserManager<ApplicationUser>> userManager)
        => new(db, userManager.Object, NullLogger<GoogleAccountAuthenticationService>.Instance);

    private static Mock<UserManager<ApplicationUser>> CreateUserManager()
        => new(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
