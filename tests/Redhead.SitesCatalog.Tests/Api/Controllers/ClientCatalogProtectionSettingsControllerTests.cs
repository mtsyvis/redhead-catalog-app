using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ClientCatalogProtectionSettingsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DateTime _now = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Get_WhenSettingsRowIsMissing_ReturnsDisabledDefaults()
    {
        // Arrange
        var sut = CreateController();

        // Act
        var result = await sut.Get(CancellationToken.None);

        // Assert
        var response = Assert.IsType<ClientCatalogProtectionSettingsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.AutoBanEnabled);
        Assert.Equal(ClientCatalogLimits.DefaultAutoBanUniqueSitesPer24Hours,
            response.AutoBanUniqueSitesPer24Hours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Update_WhenThresholdIsNotPositive_ReturnsBadRequest(int threshold)
    {
        // Arrange
        var sut = CreateController();

        // Act
        var result = await sut.Update(
            new ClientCatalogProtectionSettingsRequest(true, threshold),
            CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_db.ClientCatalogProtectionSettings);
    }

    [Fact]
    public async Task Update_WithValidSettings_PersistsAuditFields()
    {
        // Arrange
        _db.Users.Add(new ApplicationUser
        {
            Id = "superadmin",
            UserName = "owner@example.com",
            Email = "owner@example.com",
            DisplayName = "Catalog Owner"
        });
        await _db.SaveChangesAsync();
        var sut = CreateController();

        // Act
        var result = await sut.Update(
            new ClientCatalogProtectionSettingsRequest(true, 25_000),
            CancellationToken.None);

        // Assert
        var response = Assert.IsType<ClientCatalogProtectionSettingsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.AutoBanEnabled);
        Assert.Equal(25_000, response.AutoBanUniqueSitesPer24Hours);
        Assert.Equal(_now, response.UpdatedAtUtc);
        Assert.Equal("superadmin", response.UpdatedByUserId);
        Assert.Equal("Catalog Owner", response.UpdatedByDisplayName);
        Assert.Single(_db.ClientCatalogProtectionSettings);
    }

    [Fact]
    public async Task Get_WhenUpdaterDisplayNameIsBlank_ReturnsEmailForAuditLabel()
    {
        // Arrange
        _db.Users.Add(new ApplicationUser
        {
            Id = "superadmin",
            UserName = "owner@example.com",
            Email = "owner@example.com",
            DisplayName = "  "
        });
        _db.ClientCatalogProtectionSettings.Add(new ClientCatalogProtectionSettings
        {
            UpdatedByUserId = "superadmin",
            UpdatedAtUtc = _now
        });
        await _db.SaveChangesAsync();
        var sut = CreateController();

        // Act
        var result = await sut.Get(CancellationToken.None);

        // Assert
        var response = Assert.IsType<ClientCatalogProtectionSettingsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("owner@example.com", response.UpdatedByDisplayName);
    }

    [Fact]
    public void Controller_RequiresUserManagementPermissionAndSuperAdminRole()
    {
        // Arrange
        var controllerType = typeof(ClientCatalogProtectionSettingsController);

        // Act
        var attribute = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        // Assert
        Assert.Equal(AppPolicies.UsersManageAccess, attribute.Policy);
        Assert.Equal(AppRoles.SuperAdmin, attribute.Roles);
    }

    private ClientCatalogProtectionSettingsController CreateController()
        => new(_db, new FixedClock(_now))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "superadmin")], "test"))
                }
            }
        };

    public void Dispose() => _db.Dispose();

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
