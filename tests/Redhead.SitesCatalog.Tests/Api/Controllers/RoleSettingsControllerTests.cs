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
    [Theory]
    [InlineData(AppRoles.Editor)]
    [InlineData(AppRoles.Linkbuilder)]
    [InlineData(AppRoles.Lite)]
    public async Task GetRoleSettings_WhenRoleHasFixedDisabledExport_ReturnsFixedDisabledSetting(string role)
    {
        // Arrange
        await using var db = CreateDbContext();
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = role,
            ExportLimitMode = ExportLimitMode.Unlimited,
            ExportLimitRows = 100,
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
        var fixedRole = Assert.Single(items);
        Assert.Equal(role, fixedRole.Role);
        Assert.Equal(ExportLimitMode.Disabled, fixedRole.ExportLimitMode);
        Assert.Null(fixedRole.ExportLimitRows);
        Assert.False(fixedRole.IsEditable);
        Assert.Null(fixedRole.DailyUniqueExportedDomainsLimit);
        Assert.Null(fixedRole.WeeklyUniqueExportedDomainsLimit);
        Assert.Null(fixedRole.DailyExportOperationsLimit);
        Assert.Null(fixedRole.WeeklyExportOperationsLimit);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
