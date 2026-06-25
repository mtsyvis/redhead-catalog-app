using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public class RoleSettingsControllerTests
{
    [Fact]
    public async Task GetRoleSettings_WhenRoleIsLite_ReturnsFixedDisabledSetting()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = AppRoles.Lite,
            ExportLimitMode = ExportLimitMode.Disabled,
            ExportLimitRows = null,
            DailyUniqueExportedDomainsLimit = 100,
            WeeklyUniqueExportedDomainsLimit = 200,
            DailyExportOperationsLimit = 10,
            WeeklyExportOperationsLimit = 20
        });
        await db.SaveChangesAsync();
        var sut = new RoleSettingsController(db);

        // Act
        var result = await sut.GetRoleSettings(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<RoleSettingItemDto>>(ok.Value);
        var lite = Assert.Single(items);
        Assert.Equal(AppRoles.Lite, lite.Role);
        Assert.Equal(ExportLimitMode.Disabled, lite.ExportLimitMode);
        Assert.Null(lite.ExportLimitRows);
        Assert.False(lite.IsEditable);
        Assert.Null(lite.DailyUniqueExportedDomainsLimit);
        Assert.Null(lite.WeeklyUniqueExportedDomainsLimit);
        Assert.Null(lite.DailyExportOperationsLimit);
        Assert.Null(lite.WeeklyExportOperationsLimit);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
