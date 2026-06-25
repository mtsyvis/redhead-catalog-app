using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models.TableViews;
using Redhead.SitesCatalog.Application.Models.TableViews;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class MeTableViewsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        // Arrange
        var controllerType = typeof(MeTableViewsController);

        // Act
        var authorizeAttribute = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .SingleOrDefault();

        // Assert
        Assert.NotNull(authorizeAttribute);
    }

    [Fact]
    public async Task GetTableViews_WhenCurrentUserMissing_ReturnsUnauthorized()
    {
        // Arrange
        var userManager = CreateUserManagerForCurrentUser(null);
        var service = new Mock<IUserTableViewsService>();
        var sut = new MeTableViewsController(userManager.Object, service.Object);

        // Act
        var result = await sut.GetTableViews("sites", CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        service.Verify(
            item => item.GetTableViewsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTableViews_WhenUserIsLite_ReturnsTableViews()
    {
        // Arrange
        var user = new ApplicationUser { Id = "lite-1", Email = "lite@example.com" };
        var userManager = CreateUserManagerForCurrentUser(user);
        var service = new Mock<IUserTableViewsService>();
        service
            .Setup(item => item.GetTableViewsAsync("lite-1", "sites", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TableViewsResponseDto("system", "default", []));
        var sut = new MeTableViewsController(userManager.Object, service.Object);
        SetUser(sut, AppRoles.Lite);

        // Act
        var result = await sut.GetTableViews("sites", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TableViewsResponseDto>(ok.Value);
        Assert.Equal("system", payload.ActiveViewType);
        Assert.Equal("default", payload.ActiveViewKey);
    }

    [Fact]
    public async Task SetActiveView_WhenUserIsLite_UpdatesActiveView()
    {
        // Arrange
        var user = new ApplicationUser { Id = "lite-1", Email = "lite@example.com" };
        var userManager = CreateUserManagerForCurrentUser(user);
        var service = new Mock<IUserTableViewsService>();
        var sut = new MeTableViewsController(userManager.Object, service.Object);
        SetUser(sut, AppRoles.Lite);

        // Act
        var result = await sut.SetActiveView(
            "sites",
            new SetActiveTableViewRequest("system", "pricing"),
            CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        service.Verify(
            item => item.SetActiveViewAsync(
                "lite-1",
                "sites",
                "system",
                "pricing",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateCustomView_WhenUserIsLite_CreatesCustomView()
    {
        // Arrange
        var user = new ApplicationUser { Id = "lite-1", Email = "lite@example.com" };
        var userManager = CreateUserManagerForCurrentUser(user);
        var service = new Mock<IUserTableViewsService>();
        var settings = CreateSettings();
        var created = CreateCustomView("Lite view", settings);
        service
            .Setup(item => item.CreateCustomViewAsync(
                "lite-1",
                "sites",
                "Lite view",
                settings,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        var sut = new MeTableViewsController(userManager.Object, service.Object);
        SetUser(sut, AppRoles.Lite);

        // Act
        var result = await sut.CreateCustomView(
            "sites",
            new CreateTableCustomViewRequest("Lite view", settings),
            CancellationToken.None);

        // Assert
        var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(MeTableViewsController.GetTableViews), createdAt.ActionName);
        Assert.Same(created, createdAt.Value);
    }

    [Fact]
    public async Task UpdateCustomView_WhenUserIsLite_UpdatesCustomView()
    {
        // Arrange
        var user = new ApplicationUser { Id = "lite-1", Email = "lite@example.com" };
        var userManager = CreateUserManagerForCurrentUser(user);
        var service = new Mock<IUserTableViewsService>();
        var id = Guid.NewGuid();
        var settings = CreateSettings();
        var updated = CreateCustomView("Updated Lite view", settings, id);
        service
            .Setup(item => item.UpdateCustomViewAsync(
                "lite-1",
                "sites",
                id,
                "Updated Lite view",
                settings,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);
        var sut = new MeTableViewsController(userManager.Object, service.Object);
        SetUser(sut, AppRoles.Lite);

        // Act
        var result = await sut.UpdateCustomView(
            "sites",
            id,
            new UpdateTableCustomViewRequest("Updated Lite view", settings),
            CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(updated, ok.Value);
    }

    [Fact]
    public async Task DeleteCustomView_WhenUserIsLite_DeletesCustomView()
    {
        // Arrange
        var user = new ApplicationUser { Id = "lite-1", Email = "lite@example.com" };
        var userManager = CreateUserManagerForCurrentUser(user);
        var service = new Mock<IUserTableViewsService>();
        var id = Guid.NewGuid();
        var sut = new MeTableViewsController(userManager.Object, service.Object);
        SetUser(sut, AppRoles.Lite);

        // Act
        var result = await sut.DeleteCustomView("sites", id, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        service.Verify(
            item => item.DeleteCustomViewAsync("lite-1", "sites", id, It.IsAny<CancellationToken>()),
            Times.Once);
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

    private static void SetUser(MeTableViewsController controller, string role)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "lite-1"),
                        new Claim(ClaimTypes.Role, role)
                    ],
                    "Test"))
            }
        };
    }

    private static TableViewSettingsDto CreateSettings()
    {
        return new TableViewSettingsDto
        {
            SchemaVersion = 1,
            VisibleColumnIds = ["domain", "traffic"],
            Density = "standard",
            ColumnWidths = new Dictionary<string, int>
            {
                ["domain"] = 240,
                ["traffic"] = 120
            }
        };
    }

    private static TableCustomViewDto CreateCustomView(
        string name,
        TableViewSettingsDto settings,
        Guid? id = null)
    {
        return new TableCustomViewDto(
            id ?? Guid.NewGuid(),
            name,
            settings.SchemaVersion,
            settings,
            new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc));
    }
}
