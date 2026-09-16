using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
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
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(),
            new ClientCatalogBurstLimiter(TimeProvider.System, 1000));

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
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(),
            new ClientCatalogBurstLimiter(TimeProvider.System, 1000));

        // Act
        var get = await sut.Get(user.Id, CancellationToken.None);
        var update = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(300));

        // Assert
        Assert.IsType<BadRequestObjectResult>(get.Result);
        Assert.IsType<BadRequestObjectResult>(update);
        Assert.Null(user.ClientSelectionLimitOverride);
    }

    [Theory]
    [InlineData(100, 101, true)]
    [InlineData(100, 5000, true)]
    [InlineData(101, 100, true)]
    [InlineData(5000, null, true)]
    [InlineData(100, 50, false)]
    [InlineData(50, null, false)]
    public async Task Update_TrustBoundary_ClearsCounter_OnlyWhenTrustIsInvolved(int previousLimit, int? nextLimit, bool resets)
    {
        // Arrange
        var limiter = new ClientCatalogBurstLimiter(TimeProvider.System, 1000);
        var user = new ApplicationUser { ClientSelectionLimitOverride = previousLimit };
        limiter.EnsureAllowed(user.Id,
            Enumerable.Range(0, 1000).Select(index => $"site{index}.com").ToArray(), 100);
        var users = CreateUsers(user, AppRoles.Client);
        users.Setup(manager => manager.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(), limiter);

        // Act
        var response = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(nextLimit));
        var counterResult = Record.Exception(() => limiter.EnsureAllowed(user.Id, ["new.com"], 100));

        // Assert
        Assert.IsType<NoContentResult>(response);
        Assert.Equal(nextLimit, user.ClientSelectionLimitOverride);
        if (resets) Assert.Null(counterResult);
        else Assert.IsType<ClientCatalogBurstLimitExceededException>(counterResult);
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
