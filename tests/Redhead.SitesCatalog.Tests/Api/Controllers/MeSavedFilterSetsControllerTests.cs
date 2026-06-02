using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class MeSavedFilterSetsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        // Arrange
        var controllerType = typeof(MeSavedFilterSetsController);

        // Act
        var authorizeAttribute = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .SingleOrDefault();

        // Assert
        Assert.NotNull(authorizeAttribute);
    }

    [Fact]
    public async Task GetFilterSets_WhenCurrentUserMissing_ReturnsUnauthorized()
    {
        // Arrange
        var userManager = CreateUserManagerForCurrentUser(null);
        var service = new Mock<IUserSavedFilterSetsService>();
        var sut = new MeSavedFilterSetsController(userManager.Object, service.Object);

        // Act
        var result = await sut.GetFilterSets("sites", CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        service.Verify(
            item => item.GetFilterSetsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerForCurrentUser(ApplicationUser? user)
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
}
