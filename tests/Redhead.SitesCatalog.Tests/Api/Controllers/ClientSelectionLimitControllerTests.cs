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
            new ClientCatalogBurstLimiter(TimeProvider.System, 1000), TimeProvider.System);

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
            new ClientCatalogBurstLimiter(TimeProvider.System, 1000), TimeProvider.System);

        // Act
        var get = await sut.Get(user.Id, CancellationToken.None);
        var update = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(300));

        // Assert
        Assert.IsType<BadRequestObjectResult>(get.Result);
        Assert.IsType<BadRequestObjectResult>(update);
        Assert.Null(user.ClientSelectionLimitOverride);
    }

    [Theory]
    [InlineData(100, 101, true, false)]
    [InlineData(100, 5000, true, false)]
    [InlineData(101, 100, true, true)]
    [InlineData(5000, null, true, true)]
    [InlineData(100, 50, false, false)]
    [InlineData(50, null, false, false)]
    public async Task Update_TrustBoundary_ResetsApplicableProtectionWindows(
        int previousLimit,
        int? nextLimit,
        bool resetsBurst,
        bool resetsAutoBanWindow)
    {
        // Arrange
        var clock = new FixedClock();
        var limiter = new ClientCatalogBurstLimiter(clock, 1000);
        var user = new ApplicationUser { ClientSelectionLimitOverride = previousLimit };
        limiter.EnsureAllowed(user.Id,
            Enumerable.Range(0, 1000).Select(index => $"site{index}.com").ToArray(), 100);
        var users = CreateUsers(user, AppRoles.Client);
        users.Setup(manager => manager.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(), limiter, clock);

        // Act
        var response = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(nextLimit));
        var counterResult = Record.Exception(() => limiter.EnsureAllowed(user.Id, ["new.com"], 100));

        // Assert
        Assert.IsType<NoContentResult>(response);
        Assert.Equal(nextLimit, user.ClientSelectionLimitOverride);
        if (resetsBurst) Assert.Null(counterResult);
        else Assert.IsType<ClientCatalogBurstLimitExceededException>(counterResult);
        if (resetsAutoBanWindow) Assert.Equal(FixedClock.Now.UtcDateTime, user.ClientCatalogAutoBanResetAtUtc);
        else Assert.Null(user.ClientCatalogAutoBanResetAtUtc);
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

    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
