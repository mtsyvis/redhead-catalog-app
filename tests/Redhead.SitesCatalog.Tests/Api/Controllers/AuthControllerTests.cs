using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.AccountSetup;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Security;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_WhenProfileIsIncomplete_ReturnsMustCompleteProfileAndEmailDisplayName()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: true, firstName: null, lastName: null);
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync(user.Email!))
            .ReturnsAsync(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        userManager.Setup(manager => manager.HasPasswordAsync(user))
            .ReturnsAsync(true);
        var signInManager = CreateSignInManager(userManager);
        signInManager.Setup(manager => manager.PasswordSignInAsync(
                user.UserName!,
                "Password123!",
                false,
                true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        var sut = CreateController(userManager, signInManager);

        // Act
        var result = await sut.Login(new LoginRequest(user.Email!, "Password123!"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LoginResponse>(ok.Value);
        Assert.True(payload.MustChangePassword);
        Assert.True(payload.MustCompleteProfile);
        Assert.Equal(user.Email, payload.DisplayName);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenPasswordAndProfileRequired_ChangesPasswordAndTrimsNames()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: true, firstName: null, lastName: null);
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.ChangePasswordAsync(user, "Temp123!", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Internal });
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            "Temp123!",
            "NewPassword123!",
            "  Ada Lovelace  "));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.False(user.MustChangePassword);
        Assert.Equal("Ada Lovelace", user.DisplayName);
        Assert.False(payload.MustChangePassword);
        Assert.False(payload.MustCompleteProfile);
        Assert.Equal("Ada Lovelace", payload.DisplayName);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenOnlyProfileRequired_UpdatesProfileWithoutChangingPassword()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: false, firstName: null, lastName: null);
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            null,
            null,
            "  Grace Hopper  "));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Grace Hopper", user.DisplayName);
        Assert.False(payload.MustCompleteProfile);
        userManager.Verify(
            manager => manager.ChangePasswordAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenOnlyPasswordRequired_ChangesPasswordAndKeepsProfile()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: true, firstName: "Jane", lastName: "Smith");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.ChangePasswordAsync(user, "Temp123!", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Admin });
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            "Temp123!",
            "NewPassword123!",
            null));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.False(user.MustChangePassword);
        Assert.Equal("Jane Smith", user.DisplayName);
        Assert.False(payload.MustCompleteProfile);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenOnlyPasswordRequired_IgnoresProfileFields()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: true, firstName: "Jane", lastName: "Smith");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.ChangePasswordAsync(user, "Temp123!", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Admin });
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            "Temp123!",
            "NewPassword123!",
            "Grace Hopper"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Jane Smith", user.DisplayName);
        Assert.Equal("Jane Smith", payload.DisplayName);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenNoSetupIsRequired_DoesNotUpdateUser()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: false, firstName: "Jane", lastName: "Smith");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Internal });
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            "Temp123!",
            "NewPassword123!",
            "Grace Hopper"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Jane Smith", user.DisplayName);
        Assert.False(payload.MustChangePassword);
        Assert.False(payload.MustCompleteProfile);
        userManager.Verify(manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        userManager.Verify(
            manager => manager.ChangePasswordAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteAccountSetup_WhenProfileNamesAreWhitespace_ReturnsFieldErrors()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: false, firstName: null, lastName: null);
        var userManager = CreateUserManagerForCurrentUser(user);
        var sut = CreateController(userManager);

        // Act
        var result = await sut.CompleteAccountSetup(new CompleteAccountSetupRequest(
            null,
            null,
            "   "));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("displayName", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsIsExportDisabled()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: false, firstName: "Ada", lastName: "Lovelace");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        userManager.Setup(manager => manager.HasPasswordAsync(user))
            .ReturnsAsync(true);
        var policyService = CreatePolicyService(
            user,
            AppRoles.Client,
            new EffectiveExportPolicy(
                ExportLimitMode.Disabled,
                null,
                false,
                EffectivePolicySource.Role));
        var sut = CreateController(userManager, CreateSignInManager(userManager), policyService);

        // Act
        var result = await sut.GetCurrentUser();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UserInfoResponse>(ok.Value);
        Assert.True(payload.IsExportDisabled);
        Assert.True(payload.CanChangePassword);
        policyService.Verify(
            service => service.GetEffectivePolicyAsync(user, AppRoles.Client, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_WhenGoogleAvatarClaimExists_ReturnsAvatarUrl()
    {
        // Arrange
        const string avatarUrl = "https://lh3.googleusercontent.com/avatar";
        var user = CreateUser(mustChangePassword: false, firstName: "Ada", lastName: "Lovelace");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Lite });
        userManager.Setup(manager => manager.HasPasswordAsync(user))
            .ReturnsAsync(false);
        var sut = CreateController(userManager);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(AppClaimTypes.GoogleAvatarUrl, avatarUrl)],
                    "Test"))
            }
        };

        // Act
        var result = await sut.GetCurrentUser();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UserInfoResponse>(ok.Value);
        Assert.Equal(avatarUrl, payload.AvatarUrl);
    }

    [Fact]
    public async Task ChangePassword_WhenUserIsGoogleOnly_ReturnsBadRequest()
    {
        // Arrange
        var user = CreateUser(mustChangePassword: false, firstName: "Ada", lastName: "Lovelace");
        var userManager = CreateUserManagerForCurrentUser(user);
        userManager.Setup(manager => manager.HasPasswordAsync(user))
            .ReturnsAsync(false);
        var sut = CreateController(userManager);

        // Act
        var result = await sut.ChangePassword(new ChangePasswordRequest(
            "CurrentPassword123!",
            "NewPassword123!"));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains("Google sign-in", payload.Message);
        userManager.Verify(
            manager => manager.ChangePasswordAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ActivateAccount_WhenInvitationIsValid_SetsPasswordProfileAndSignsIn()
    {
        // Arrange
        const string token = "valid-invitation-token";
        var user = new ApplicationUser
        {
            Id = "invited-user",
            UserName = "invited@example.com",
            Email = "invited@example.com",
            IsActive = true,
            InvitationTokenHash = UserInvitationToken.Hash(token),
            InvitationExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        var userManager = CreateUserManager();
        userManager.SetupGet(manager => manager.Users).Returns(new[] { user }.AsQueryable());
        userManager.Setup(manager => manager.AddPasswordAsync(user, "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var signInManager = CreateSignInManager(userManager);
        signInManager.Setup(manager => manager.SignInAsync(user, false, null))
            .Returns(Task.CompletedTask);
        var sut = CreateController(userManager, signInManager);

        // Act
        var result = await sut.ActivateAccount(new ActivateAccountRequest(
            token,
            "  Ada Lovelace  ",
            "NewPassword123!"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ActivateAccountResponse>(ok.Value);
        Assert.Equal("Ada Lovelace", payload.DisplayName);
        Assert.Equal("Ada Lovelace", user.DisplayName);
        Assert.NotNull(user.ActivatedAtUtc);
        Assert.Null(user.InvitationTokenHash);
        Assert.Null(user.InvitationExpiresAtUtc);
        Assert.False(user.MustChangePassword);
        signInManager.Verify(manager => manager.SignInAsync(user, false, null), Times.Once);
    }

    [Fact]
    public async Task ActivateAccount_WhenInvitationExpired_DoesNotSetPassword()
    {
        // Arrange
        const string token = "expired-invitation-token";
        var user = new ApplicationUser
        {
            Id = "invited-user",
            UserName = "invited@example.com",
            Email = "invited@example.com",
            IsActive = true,
            InvitationTokenHash = UserInvitationToken.Hash(token),
            InvitationExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var userManager = CreateUserManager();
        userManager.SetupGet(manager => manager.Users).Returns(new[] { user }.AsQueryable());
        var sut = CreateController(userManager);

        // Act
        var result = await sut.ActivateAccount(new ActivateAccountRequest(
            token,
            "Ada Lovelace",
            "NewPassword123!"));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        userManager.Verify(
            manager => manager.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_WhenReactivationIsPending_DoesNotTryOldPassword()
    {
        // Arrange
        var user = CreateReactivatingUser("reactivation-token");
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync(user.Email!))
            .ReturnsAsync(user);
        var signInManager = CreateSignInManager(userManager);
        var sut = CreateController(userManager, signInManager);

        // Act
        var result = await sut.Login(new LoginRequest(user.Email!, "OldPassword123!"));

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        signInManager.Verify(manager => manager.PasswordSignInAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReactivateAccount_WhenLinkIsValid_ReplacesPasswordActivatesAndSignsIn()
    {
        // Arrange
        const string token = "valid-reactivation-token";
        var user = CreateReactivatingUser(token);
        var userManager = CreateUserManager();
        userManager.SetupGet(manager => manager.Users).Returns(new[] { user }.AsQueryable());
        userManager.Setup(manager => manager.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("identity-reset-token");
        userManager.Setup(manager => manager.ResetPasswordAsync(user, "identity-reset-token", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Client });
        var signInManager = CreateSignInManager(userManager);
        signInManager.Setup(manager => manager.SignInAsync(user, false, null))
            .Returns(Task.CompletedTask);
        var sut = CreateController(userManager, signInManager);

        // Act
        var result = await sut.ReactivateAccount(new ReactivateAccountRequest(token, "NewPassword123!"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ReactivateAccountResponse>(ok.Value);
        Assert.Equal(user.Email, payload.Email);
        Assert.True(user.IsActive);
        Assert.False(user.MustChangePassword);
        Assert.Null(user.InvitationTokenHash);
        Assert.Null(user.InvitationExpiresAtUtc);
        userManager.Verify(
            manager => manager.ResetPasswordAsync(user, "identity-reset-token", "NewPassword123!"),
            Times.Once);
        signInManager.Verify(manager => manager.SignInAsync(user, false, null), Times.Once);

        var repeatedResult = await sut.ReactivateAccount(new ReactivateAccountRequest(token, "OtherPassword123!"));
        Assert.IsType<NotFoundObjectResult>(repeatedResult.Result);
    }

    [Fact]
    public async Task ReactivateAccount_WhenPasswordIsRejected_KeepsUserDisabledAndLinkUsable()
    {
        // Arrange
        const string token = "valid-reactivation-token";
        var user = CreateReactivatingUser(token);
        user.MustChangePassword = true;
        var userManager = CreateUserManager();
        userManager.SetupGet(manager => manager.Users).Returns(new[] { user }.AsQueryable());
        userManager.Setup(manager => manager.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("identity-reset-token");
        userManager.Setup(manager => manager.ResetPasswordAsync(user, "identity-reset-token", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password is too weak." }));
        var sut = CreateController(userManager);

        // Act
        var result = await sut.ReactivateAccount(new ReactivateAccountRequest(token, "weak"));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.Equal(UserInvitationToken.Hash(token), user.InvitationTokenHash);
        Assert.NotNull(user.InvitationExpiresAtUtc);
    }

    [Fact]
    public async Task ReactivateAccount_WhenLinkIsExpired_DoesNotChangePassword()
    {
        // Arrange
        const string token = "expired-reactivation-token";
        var user = CreateReactivatingUser(token);
        user.InvitationExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var userManager = CreateUserManager();
        userManager.SetupGet(manager => manager.Users).Returns(new[] { user }.AsQueryable());
        var sut = CreateController(userManager);

        // Act
        var result = await sut.ReactivateAccount(new ReactivateAccountRequest(token, "NewPassword123!"));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(user.IsActive);
        userManager.Verify(
            manager => manager.ResetPasswordAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void AuthResponses_DoNotExposeSuperAdminNote()
    {
        // Arrange
        var responseTypes = new[]
        {
            typeof(LoginResponse),
            typeof(CompleteAccountSetupResponse),
            typeof(UserInfoResponse)
        };

        // Act
        var propertyNames = responseTypes
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .ToList();

        // Assert
        Assert.DoesNotContain("SuperAdminNote", propertyNames);
    }

    private static AuthController CreateController(Mock<UserManager<ApplicationUser>> userManager)
        => CreateController(userManager, CreateSignInManager(userManager));

    private static AuthController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<SignInManager<ApplicationUser>> signInManager)
        => CreateController(userManager, signInManager, CreatePolicyService());

    private static AuthController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<SignInManager<ApplicationUser>> signInManager,
        Mock<IEffectiveExportPolicyService> effectiveExportPolicyService)
        => new(
            userManager.Object,
            signInManager.Object,
            new AccountSetupService(
                userManager.Object,
                NullLogger<AccountSetupService>.Instance),
            effectiveExportPolicyService.Object,
            NullLogger<AuthController>.Instance);

    private static Mock<IEffectiveExportPolicyService> CreatePolicyService()
    {
        var policyService = new Mock<IEffectiveExportPolicyService>();
        policyService
            .Setup(service => service.GetEffectivePolicyAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveExportPolicy(
                ExportLimitMode.Unlimited,
                null,
                false,
                EffectivePolicySource.Role));
        return policyService;
    }

    private static Mock<IEffectiveExportPolicyService> CreatePolicyService(
        ApplicationUser user,
        string role,
        EffectiveExportPolicy policy)
    {
        var policyService = CreatePolicyService();
        policyService
            .Setup(service => service.GetEffectivePolicyAsync(user, role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        return policyService;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerForCurrentUser(ApplicationUser user)
    {
        var userManager = CreateUserManager();
        userManager.Setup(manager => manager.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        return userManager;
    }

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

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(
        Mock<UserManager<ApplicationUser>> userManager)
        => new(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());

    private static ApplicationUser CreateUser(
        bool mustChangePassword,
        string? firstName,
        string? lastName)
        => new()
        {
            Id = "user-1",
            UserName = "user@example.com",
            Email = "user@example.com",
            MustChangePassword = mustChangePassword,
            DisplayName = string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
                ? null
                : $"{firstName} {lastName}",
            ActivatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

    private static ApplicationUser CreateReactivatingUser(string token)
        => new()
        {
            Id = "returning-user",
            UserName = "returning@example.com",
            Email = "returning@example.com",
            DisplayName = "Ada Lovelace",
            ActivatedAtUtc = DateTime.UtcNow.AddDays(-30),
            IsActive = false,
            InvitationTokenHash = UserInvitationToken.Hash(token),
            InvitationExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
}
