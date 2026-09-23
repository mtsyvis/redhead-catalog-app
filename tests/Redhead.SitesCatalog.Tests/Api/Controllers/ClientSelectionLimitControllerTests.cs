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
    [InlineData(101)]
    public async Task Update_InvalidLimit_RejectsWithoutChangingUser(int limit)
    {
        // Arrange
        using var db = CreateContext();
        var user = new ApplicationUser { ClientSelectionLimitOverride = 50 };
        var users = CreateUsers(user, AppRoles.Client);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(),
            db, new ClientCatalogBurstLimiter(TimeProvider.System, 1000), TimeProvider.System);

        // Act
        var result = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(limit, false));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(50, user.ClientSelectionLimitOverride);
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
            db, new ClientCatalogBurstLimiter(TimeProvider.System, 1000), TimeProvider.System);

        // Act
        var get = await sut.Get(user.Id, CancellationToken.None);
        var update = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(50, false));

        // Assert
        Assert.IsType<BadRequestObjectResult>(get.Result);
        Assert.IsType<BadRequestObjectResult>(update);
        Assert.Null(user.ClientSelectionLimitOverride);
    }

    [Theory]
    [InlineData(false, true, 100, null, true, false)]
    [InlineData(true, false, null, 100, true, true)]
    [InlineData(false, false, 100, 50, false, false)]
    public async Task Update_TrustBoundary_ResetsApplicableProtectionWindows(
        bool previousTrusted,
        bool nextTrusted,
        int? previousLimit,
        int? nextLimit,
        bool resetsBurst,
        bool resetsAutoBanWindow)
    {
        // Arrange
        using var db = CreateContext();
        var clock = new FixedClock();
        var limiter = new ClientCatalogBurstLimiter(clock, 1000);
        var user = new ApplicationUser
        {
            ClientSelectionLimitOverride = previousLimit,
            IsTrustedClient = previousTrusted
        };
        limiter.EnsureAllowed(user.Id,
            Enumerable.Range(0, 1000).Select(index => $"site{index}.com").ToArray(), 100);
        var users = CreateUsers(user, AppRoles.Client);
        users.Setup(manager => manager.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(), db, limiter, clock);

        // Act
        var response = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(nextLimit, nextTrusted));
        var counterResult = Record.Exception(() => limiter.EnsureAllowed(user.Id, ["new.com"], 100));

        // Assert
        Assert.IsType<NoContentResult>(response);
        Assert.Equal(nextTrusted ? null : nextLimit, user.ClientSelectionLimitOverride);
        Assert.Equal(nextTrusted, user.IsTrustedClient);
        if (resetsBurst) Assert.Null(counterResult);
        else Assert.IsType<ClientCatalogBurstLimitExceededException>(counterResult);
        if (resetsAutoBanWindow) Assert.Equal(FixedClock.Now.UtcDateTime, user.ClientCatalogAutoBanResetAtUtc);
        else Assert.Null(user.ClientCatalogAutoBanResetAtUtc);
    }

    [Fact]
    public async Task Update_EnableTrust_ClosesOpenHourlyAlert()
    {
        // Arrange
        using var db = CreateContext();
        var clock = new FixedClock();
        var user = new ApplicationUser { Id = "client", ClientSelectionLimitOverride = 100 };
        var alert = new ClientCatalogAlert
        {
            UserId = user.Id,
            DetectedAtUtc = FixedClock.Now.UtcDateTime,
            UniqueSites = 5000,
            Threshold = 5000,
            NextEmailAttemptAtUtc = FixedClock.Now.UtcDateTime
        };
        db.ClientCatalogAlerts.Add(alert);
        await db.SaveChangesAsync();
        var users = CreateUsers(user, AppRoles.Client);
        users.Setup(manager => manager.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = new ClientSelectionLimitController(users.Object, Mock.Of<IClientCatalogActivityService>(), db,
            new ClientCatalogBurstLimiter(clock, 1000), clock);

        // Act
        var response = await sut.Update(user.Id, new ClientSelectionLimitController.UpdateRequest(null, true));

        // Assert
        Assert.IsType<NoContentResult>(response);
        Assert.Equal(FixedClock.Now.UtcDateTime, alert.ReviewedAtUtc);
        Assert.Null(alert.ReviewedByUserId);
        Assert.Null(alert.NextEmailAttemptAtUtc);
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
