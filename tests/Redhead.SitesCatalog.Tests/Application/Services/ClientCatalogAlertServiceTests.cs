using Redhead.SitesCatalog.Domain.ClientCatalog;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests;

public sealed class ClientCatalogAlertServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly MutableClock _clock = new();
    private readonly Mock<IClientCatalogAlertEmailSender> _sender = new();
    private readonly ClientCatalogOptions _options = new() { AlertEmails = "admin@example.com" };

    public ClientCatalogAlertServiceTests()
    {
        _db.Users.Add(new ApplicationUser { Id = "client", Email = "client@example.com" });
        _db.Roles.Add(new IdentityRole { Id = "client-role", Name = AppRoles.Client });
        _db.UserRoles.Add(new IdentityUserRole<string> { UserId = "client", RoleId = "client-role" });
        _db.SaveChanges();
        _sender.Setup(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sender.Setup(sender => sender.SendAutoBanAsync(It.IsAny<ClientCatalogAutoBan>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AutoBan_Enabled_AppliesOnlyToProtectedClients(bool isTrustedClient, bool shouldBan)
    {
        // Arrange
        _db.Users.Single().IsTrustedClient = isTrustedClient;
        _db.ClientCatalogProtectionSettings.Add(new ClientCatalogProtectionSettings
        {
            AutoBanEnabled = true,
            AutoBanUniqueSitesPer24Hours = 3
        });
        AddActivity(3);
        await _db.SaveChangesAsync();
        var sut = CreateAutoBanService();

        // Act
        await sut.ProcessAsync(CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        var user = await _db.Users.SingleAsync();
        Assert.Equal(!shouldBan, user.IsActive);
        Assert.Equal(shouldBan ? UserDisabledReasons.ClientCatalogAutoBan : null, user.DisabledReason);
        Assert.Equal(shouldBan ? 1 : 0, await _db.ClientCatalogAutoBans.CountAsync());
        _sender.Verify(sender => sender.SendAutoBanAsync(
            It.IsAny<ClientCatalogAutoBan>(),
            "client@example.com",
            It.IsAny<CancellationToken>()),
            shouldBan ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task AutoBan_Disabled_DoesNotDisableClient()
    {
        // Arrange
        _db.ClientCatalogProtectionSettings.Add(new ClientCatalogProtectionSettings
        {
            AutoBanEnabled = false,
            AutoBanUniqueSitesPer24Hours = 3
        });
        AddActivity(3);
        await _db.SaveChangesAsync();

        // Act
        await CreateAutoBanService().ProcessAsync(CancellationToken.None);

        // Assert
        Assert.True((await _db.Users.SingleAsync()).IsActive);
        Assert.Empty(_db.ClientCatalogAutoBans);
        _sender.Verify(sender => sender.SendAutoBanAsync(
            It.IsAny<ClientCatalogAutoBan>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task AutoBan_ResetTime_IgnoresEarlierActivity()
    {
        // Arrange
        AddActivity(3);
        var user = _db.Users.Single();
        user.ClientCatalogAutoBanResetAtUtc = _clock.GetUtcNow().UtcDateTime;
        _db.ClientCatalogProtectionSettings.Add(new ClientCatalogProtectionSettings
        {
            AutoBanEnabled = true,
            AutoBanUniqueSitesPer24Hours = 3
        });
        await _db.SaveChangesAsync();
        var sut = CreateAutoBanService();

        // Act
        await sut.ProcessAsync(CancellationToken.None);
        var activeAfterReset = user.IsActive;
        _clock.Advance(TimeSpan.FromMinutes(1));
        AddActivity(3);
        await _db.SaveChangesAsync();
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.True(activeAfterReset);
        Assert.False(user.IsActive);
        Assert.Single(_db.ClientCatalogAutoBans);
    }

    [Fact]
    public async Task AutoBanReview_AlsoReviewsOpenSuspiciousActivityAlert()
    {
        // Arrange
        var autoBan = new ClientCatalogAutoBan
        {
            UserId = "client",
            DetectedAtUtc = _clock.GetUtcNow().UtcDateTime,
            UniqueSites = 20_000,
            Threshold = 20_000
        };
        var alert = new ClientCatalogAlert
        {
            UserId = "client",
            DetectedAtUtc = _clock.GetUtcNow().UtcDateTime,
            UniqueSites = 5_000,
            Threshold = 5_000
        };
        _db.AddRange(autoBan, alert);
        await _db.SaveChangesAsync();
        var controller = new ClientCatalogAutoBansController(_db, _clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "admin")], "test"))
                }
            }
        };

        // Act
        await controller.Review(autoBan.Id, CancellationToken.None);

        // Assert
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, autoBan.ReviewedAtUtc);
        Assert.Equal("admin", autoBan.ReviewedByUserId);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, alert.ReviewedAtUtc);
        Assert.Equal("admin", alert.ReviewedByUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HourlyThreshold_DeduplicatesSearchAndExport_AndSendsOneEmail(bool isTrustedClient)
    {
        // Arrange
        _db.Users.Single().IsTrustedClient = isTrustedClient;
        AddActivity(4999);
        _db.ExportedDomainAccesses.Add(new ExportedDomainAccess { UserId = "client", Domain = "site1.com", ExportedAtUtc = _clock.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync();
        var sut = CreateService();

        // Act
        await sut.ProcessAsync(CancellationToken.None);
        var beforeThreshold = await _db.ClientCatalogAlerts.CountAsync();
        _db.ExportedDomainAccesses.Add(new ExportedDomainAccess { UserId = "client", Domain = "site5000.com", ExportedAtUtc = _clock.GetUtcNow().UtcDateTime });
        await _db.SaveChangesAsync();
        await sut.ProcessAsync(CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, beforeThreshold);
        var alert = await _db.ClientCatalogAlerts.SingleAsync();
        Assert.Equal(5000, alert.UniqueSites);
        Assert.NotNull(alert.EmailSentAtUtc);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), "client@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmptyRecipients_PreservesAlertForLaterDelivery()
    {
        // Arrange
        _options.AlertEmails = "";
        AddActivity(5000);
        await _db.SaveChangesAsync();
        var sut = CreateService();

        // Act
        await sut.ProcessAsync(CancellationToken.None);
        var pending = await _db.ClientCatalogAlerts.SingleAsync();
        var sentBeforeConfiguration = pending.EmailSentAtUtc;
        _options.AlertEmails = "admin@example.com";
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Null(sentBeforeConfiguration);
        Assert.NotNull(pending.EmailSentAtUtc);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmailFailure_RetriesAfterFiveMinutes_WithoutCreatingAnotherAlert()
    {
        // Arrange
        AddActivity(5000);
        await _db.SaveChangesAsync();
        _sender.SetupSequence(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false).ReturnsAsync(true);
        var sut = CreateService();

        // Act
        await sut.ProcessAsync(CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None);
        var sentAfterFailure = _db.ClientCatalogAlerts.Single().EmailSentAtUtc;
        _clock.Advance(TimeSpan.FromMinutes(5));
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Null(sentAfterFailure);
        Assert.NotNull(_db.ClientCatalogAlerts.Single().EmailSentAtUtc);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Review_AfterOneHour_WaitsForActivityToReachThreshold()
    {
        // Arrange
        AddActivity(5000);
        await _db.SaveChangesAsync();
        var sut = CreateService();
        await sut.ProcessAsync(CancellationToken.None);
        var alert = _db.ClientCatalogAlerts.Single();
        var controller = CreateController();

        // Act
        await controller.Review(alert.Id, CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None);
        var alertsAfterReview = _db.ClientCatalogAlerts.Count();
        _clock.Advance(TimeSpan.FromHours(1));
        await sut.ProcessAsync(CancellationToken.None);
        var alertsAfterOldActivityExpires = _db.ClientCatalogAlerts.Count();
        AddActivity(5000);
        await _db.SaveChangesAsync();
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, alertsAfterReview);
        Assert.Equal(1, alertsAfterOldActivityExpires);
        Assert.Equal("admin", alert.ReviewedByUserId);
        Assert.Equal(2, _db.ClientCatalogAlerts.Count());
        Assert.Single(_db.ClientCatalogAlerts.Where(item => item.ReviewedAtUtc == null));
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BurstBudget_IsSharedAcrossServices_ButResetsOnApplicationRestart()
    {
        // Arrange
        var limiter = new ClientCatalogBurstLimiter(_clock, 1000);
        var service = new ClientCatalogService(_db, limiter);
        var domains = Enumerable.Range(1, 1000).Select(i => $"site{i}.com").ToArray();
        var concurrencyStamp = _db.Users.Single().ConcurrencyStamp;

        // Act
        await service.EnsureBurstLimitAsync("client", domains, 100);
        var sameInstance = new ClientCatalogService(_db, limiter);
        var rejected = await Record.ExceptionAsync(() => sameInstance.EnsureBurstLimitAsync("client", ["new.com"], 100));
        var restarted = new ClientCatalogService(_db, new ClientCatalogBurstLimiter(_clock, 1000));
        var recovered = await Record.ExceptionAsync(() => restarted.EnsureBurstLimitAsync("client", ["new.com"], 100));

        // Assert
        Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected);
        Assert.Null(recovered);
        Assert.Equal(concurrencyStamp, _db.Users.Single().ConcurrencyStamp);
    }

    [Fact]
    public void Review_RequiresUserManagementPermission()
    {
        // Arrange
        var method = typeof(ClientCatalogAlertsController).GetMethod(nameof(ClientCatalogAlertsController.Review))!;

        // Act
        var attribute = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());

        // Assert
        Assert.Equal(AppPolicies.UsersManageAccess, attribute.Policy);
    }

    [Fact]
    public async Task Review_ContinuousHighActivity_RealertsExactlyOneHourAfterReview()
    {
        // Arrange
        AddActivity(5000);
        await _db.SaveChangesAsync();
        var sut = CreateService();
        await sut.ProcessAsync(CancellationToken.None);
        var alert = _db.ClientCatalogAlerts.Single();
        // The hour starts at review, not at the original detection time.
        _clock.Advance(TimeSpan.FromMinutes(20));
        var controller = CreateController();

        // Act
        await controller.Review(alert.Id, CancellationToken.None);
        var reviewedAt = alert.ReviewedAtUtc;
        foreach (var minutes in new[] { 30, 29 })
        {
            _clock.Advance(TimeSpan.FromMinutes(minutes));
            AddActivity(5000);
            await _db.SaveChangesAsync();
            await sut.ProcessAsync(CancellationToken.None);
        }
        _clock.Advance(TimeSpan.FromSeconds(59));
        await sut.ProcessAsync(CancellationToken.None);
        var countBeforeHour = await _db.ClientCatalogAlerts.CountAsync();
        // Retrying review must not extend the cooldown.
        await controller.Review(alert.Id, CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _db.ChangeTracker.Clear();
        await CreateService().ProcessAsync(CancellationToken.None);
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, countBeforeHour);
        Assert.Equal(2, await _db.ClientCatalogAlerts.CountAsync());
        var reviewed = await _db.ClientCatalogAlerts.SingleAsync(item => item.Id == alert.Id);
        Assert.Equal(reviewedAt, reviewed.ReviewedAtUtc);
        Assert.Equal("admin", reviewed.ReviewedByUserId);
        var next = await _db.ClientCatalogAlerts.SingleAsync(item => item.ReviewedAtUtc == null);
        Assert.Equal(reviewedAt!.Value.AddHours(1), next.DetectedAtUtc);
        Assert.Equal(5000, next.UniqueSites);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Review_ActivityDropsAndRises_DoesNotShortenCooldown()
    {
        // Arrange
        AddActivity(5000);
        await _db.SaveChangesAsync();
        var sut = CreateService();
        await sut.ProcessAsync(CancellationToken.None);
        var alert = await _db.ClientCatalogAlerts.SingleAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        var controller = CreateController();

        // Act
        await controller.Review(alert.Id, CancellationToken.None);
        await sut.ProcessAsync(CancellationToken.None); // Old activity has expired.
        _clock.Advance(TimeSpan.FromMinutes(1));
        AddActivity(5000);
        await _db.SaveChangesAsync();
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.Single(_db.ClientCatalogAlerts);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenIncident_RemainsSingle_WithoutHourlyEmailDuplicates()
    {
        // Arrange
        AddActivity(5000);
        await _db.SaveChangesAsync();
        var sut = CreateService();
        await sut.ProcessAsync(CancellationToken.None);

        // Act
        _clock.Advance(TimeSpan.FromHours(2));
        AddActivity(6000);
        await _db.SaveChangesAsync();
        await sut.ProcessAsync(CancellationToken.None);

        // Assert
        var alert = Assert.Single(_db.ClientCatalogAlerts);
        Assert.Null(alert.ReviewedAtUtc);
        Assert.Equal(6000, alert.UniqueSites);
        _sender.Verify(sender => sender.SendAsync(It.IsAny<ClientCatalogAlert>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void AddActivity(int count) => _db.ClientCatalogRequests.Add(new ClientCatalogRequest
    {
        UserId = "client", TimestampUtc = _clock.GetUtcNow().UtcDateTime, StatusCode = 200, Endpoint = "/api/sites/search",
        Domains = Enumerable.Range(1, count).Select(i => $"site{i}.com").ToArray()
    });

    private ClientCatalogAlertsController CreateController() => new(_db, _clock)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "test"))
            }
        }
    };

    private ClientCatalogAlertService CreateService() => new(_db, Options.Create(_options), _sender.Object, _clock);
    private ClientCatalogAutoBanService CreateAutoBanService() => new(_db, Options.Create(_options), _sender.Object, _clock);
    public void Dispose() => _db.Dispose();

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
