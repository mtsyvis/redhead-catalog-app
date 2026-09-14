using Redhead.SitesCatalog.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ClientSelectionLimitControllerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5001)]
    public async Task Update_InvalidLimit_RejectsWithoutChangingUser(int limit)
    {
        // Arrange
        using var db = CreateContext();
        var user = new ApplicationUser { ClientSelectionLimitOverride = 300 };
        var users = CreateUsers(user, AppRoles.Client);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>());

        // Act
        var result = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(limit));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(300, user.ClientSelectionLimitOverride);
    }

    [Theory]
    [InlineData(AppRoles.Internal)]
    [InlineData(AppRoles.Lite)]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task GetAndUpdate_NonClientTarget_Reject(string role)
    {
        // Arrange
        using var db = CreateContext();
        var user = new ApplicationUser();
        var users = CreateUsers(user, role);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>());

        // Act
        var get = await sut.Get(user.Id, CancellationToken.None);
        var update = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(300));

        // Assert
        Assert.IsType<BadRequestObjectResult>(get.Result);
        Assert.IsType<BadRequestObjectResult>(update);
        Assert.Null(user.ClientSelectionLimitOverride);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Mock<UserManager<ApplicationUser>> CreateUsers(ApplicationUser user, string role)
    {
        var users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        users.Setup(x => x.IsInRoleAsync(user, AppRoles.Client)).ReturnsAsync(role == AppRoles.Client);
        return users;
    }
}
