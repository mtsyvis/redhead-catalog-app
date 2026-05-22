using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class EffectiveExportPolicyServiceTests
{
    [Fact]
    public async Task GetEffectivePolicyAsync_UsesRoleSettingsWhenNoOverrideExists()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = AppRoles.Client,
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 5000
        });
        await db.SaveChangesAsync();
        var sut = new EffectiveExportPolicyService(db);

        // Act
        var policy = await sut.GetEffectivePolicyAsync(
            new ApplicationUser { Id = "user-1" },
            AppRoles.Client,
            CancellationToken.None);

        // Assert
        Assert.Equal(ExportLimitMode.Limited, policy.Mode);
        Assert.Equal(5000, policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.Role, policy.Source);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_UsesUserOverrideBeforeRoleSettings()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = AppRoles.Client,
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 5000
        });
        await db.SaveChangesAsync();
        var sut = new EffectiveExportPolicyService(db);
        var user = new ApplicationUser
        {
            Id = "user-1",
            ExportLimitOverrideMode = ExportLimitMode.Limited,
            ExportLimitRowsOverride = 250
        };

        // Act
        var policy = await sut.GetEffectivePolicyAsync(user, AppRoles.Client, CancellationToken.None);

        // Assert
        Assert.Equal(ExportLimitMode.Limited, policy.Mode);
        Assert.Equal(250, policy.Rows);
        Assert.True(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.UserOverride, policy.Source);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_SuperAdminIsAlwaysUnlimited()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.RoleSettings.Add(new RoleSettings
        {
            RoleName = AppRoles.SuperAdmin,
            ExportLimitMode = ExportLimitMode.Limited,
            ExportLimitRows = 10
        });
        await db.SaveChangesAsync();
        var sut = new EffectiveExportPolicyService(db);
        var user = new ApplicationUser
        {
            Id = "user-1",
            ExportLimitOverrideMode = ExportLimitMode.Disabled
        };

        // Act
        var policy = await sut.GetEffectivePolicyAsync(user, AppRoles.SuperAdmin, CancellationToken.None);

        // Assert
        Assert.Equal(ExportLimitMode.Unlimited, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.SuperAdminFixed, policy.Source);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_MissingRoleSettingsThrowsSameExceptionAsExportEnforcement()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = new EffectiveExportPolicyService(db);

        // Act
        var act = () => sut.GetEffectivePolicyAsync(
            new ApplicationUser { Id = "user-1" },
            AppRoles.Client,
            CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<RoleSettingsNotFoundException>(act);
        Assert.Equal(AppRoles.Client, exception.RoleName);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
