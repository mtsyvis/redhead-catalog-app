using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.Authentication;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class GoogleAuthenticationControllerTests
{
    [Fact]
    public async Task Callback_WhenGoogleAuthenticationSucceeds_CreatesPersistentApplicationSession()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "google-user-1",
            UserName = "user@example.com",
            Email = "user@example.com",
            IsActive = true
        };
        var authenticationProperties = new AuthenticationProperties();
        authenticationProperties.Items["returnUrl"] = "/sites?mode=multi";
        var externalLoginInfo = CreateExternalLoginInfo(authenticationProperties);
        var googleAccountAuthenticationService = new Mock<IGoogleAccountAuthenticationService>();
        googleAccountAuthenticationService
            .Setup(service => service.AuthenticateAsync(
                It.Is<GoogleIdentity>(identity =>
                    identity.Subject == "google-subject-1" &&
                    identity.Email == "user@example.com" &&
                    identity.EmailVerified),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.Success,
                user));
        var signInManager = CreateSignInManager();
        signInManager
            .Setup(manager => manager.GetExternalLoginInfoAsync(null))
            .ReturnsAsync(externalLoginInfo);
        signInManager
            .Setup(manager => manager.SignInAsync(
                user,
                true,
                ExternalLoginProviders.Google))
            .Returns(Task.CompletedTask);
        var applicationAuthenticationService = CreateApplicationAuthenticationService();
        using var serviceProvider = CreateServiceProvider(applicationAuthenticationService);
        var sut = CreateController(
            googleAccountAuthenticationService,
            signInManager,
            serviceProvider);

        // Act
        var result = await sut.Callback(CancellationToken.None);

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/sites?mode=multi", redirect.Url);
        signInManager.Verify(
            manager => manager.SignInAsync(
                user,
                true,
                ExternalLoginProviders.Google),
            Times.Once);
        applicationAuthenticationService.Verify(
            service => service.SignOutAsync(
                It.IsAny<HttpContext>(),
                IdentityConstants.ExternalScheme,
                It.IsAny<AuthenticationProperties?>()),
            Times.Once);
    }

    [Fact]
    public async Task Callback_WhenGoogleAuthenticationIsRejected_DoesNotCreateApplicationSession()
    {
        // Arrange
        var externalLoginInfo = CreateExternalLoginInfo(new AuthenticationProperties());
        var googleAccountAuthenticationService = new Mock<IGoogleAccountAuthenticationService>();
        googleAccountAuthenticationService
            .Setup(service => service.AuthenticateAsync(
                It.IsAny<GoogleIdentity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.Disabled));
        var signInManager = CreateSignInManager();
        signInManager
            .Setup(manager => manager.GetExternalLoginInfoAsync(null))
            .ReturnsAsync(externalLoginInfo);
        var applicationAuthenticationService = CreateApplicationAuthenticationService();
        using var serviceProvider = CreateServiceProvider(applicationAuthenticationService);
        var sut = CreateController(
            googleAccountAuthenticationService,
            signInManager,
            serviceProvider);

        // Act
        var result = await sut.Callback(CancellationToken.None);

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login?googleAuth=disabled", redirect.Url);
        signInManager.Verify(
            manager => manager.SignInAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<bool>(),
                It.IsAny<string?>()),
            Times.Never);
        applicationAuthenticationService.Verify(
            service => service.SignOutAsync(
                It.IsAny<HttpContext>(),
                IdentityConstants.ExternalScheme,
                It.IsAny<AuthenticationProperties?>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/sites", "/sites")]
    [InlineData("/sites?mode=multi#results", "/sites?mode=multi#results")]
    [InlineData("https://evil.example", "/sites")]
    [InlineData("//evil.example", "/sites")]
    [InlineData("/\\evil.example", "/sites")]
    [InlineData("/sites\r\nLocation:https://evil.example", "/sites")]
    [InlineData(null, "/sites")]
    public void NormalizeReturnUrl_OnlyAllowsLocalPaths(string? returnUrl, string expected)
    {
        // Arrange

        // Act
        var result = GoogleAuthenticationController.NormalizeReturnUrl(returnUrl);

        // Assert
        Assert.Equal(expected, result);
    }

    private static GoogleAuthenticationController CreateController(
        Mock<IGoogleAccountAuthenticationService> googleAccountAuthenticationService,
        Mock<SignInManager<ApplicationUser>> signInManager,
        IServiceProvider serviceProvider)
    {
        var controller = new GoogleAuthenticationController(
            googleAccountAuthenticationService.Object,
            signInManager.Object,
            Options.Create(new GoogleAuthenticationOptions { Enabled = true }),
            Options.Create(new FrontendOptions()),
            NullLogger<GoogleAuthenticationController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = serviceProvider
                }
            }
        };

        return controller;
    }

    private static ExternalLoginInfo CreateExternalLoginInfo(AuthenticationProperties properties)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(GoogleAuthenticationController.EmailVerifiedClaimType, bool.TrueString)
        ], ExternalLoginProviders.Google));

        return new ExternalLoginInfo(
            principal,
            ExternalLoginProviders.Google,
            "google-subject-1",
            "Google")
        {
            AuthenticationProperties = properties
        };
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager()
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

        return new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }

    private static Mock<IAuthenticationService> CreateApplicationAuthenticationService()
    {
        var service = new Mock<IAuthenticationService>();
        service
            .Setup(authenticationService => authenticationService.SignOutAsync(
                It.IsAny<HttpContext>(),
                IdentityConstants.ExternalScheme,
                It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);
        return service;
    }

    private static ServiceProvider CreateServiceProvider(
        Mock<IAuthenticationService> authenticationService)
        => new ServiceCollection()
            .AddSingleton(authenticationService.Object)
            .BuildServiceProvider();
}
