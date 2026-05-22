using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

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
        Assert.Null(payload.FirstName);
        Assert.Null(payload.LastName);
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
            "  Ada  ",
            "  Lovelace  "));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.False(user.MustChangePassword);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
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
            "  Grace  ",
            "  Hopper  "));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Grace", user.FirstName);
        Assert.Equal("Hopper", user.LastName);
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
            null,
            null));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.False(user.MustChangePassword);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
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
            "Grace",
            "Hopper"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
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
            "Grace",
            "Hopper"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CompleteAccountSetupResponse>(ok.Value);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
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
            "   ",
            "   "));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("firstName", problem.Errors.Keys);
        Assert.Contains("lastName", problem.Errors.Keys);
    }

    private static AuthController CreateController(Mock<UserManager<ApplicationUser>> userManager)
        => CreateController(userManager, CreateSignInManager(userManager));

    private static AuthController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<SignInManager<ApplicationUser>> signInManager)
        => new(
            userManager.Object,
            signInManager.Object,
            new AccountSetupService(
                userManager.Object,
                NullLogger<AccountSetupService>.Instance),
            NullLogger<AuthController>.Instance);

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
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };
}
