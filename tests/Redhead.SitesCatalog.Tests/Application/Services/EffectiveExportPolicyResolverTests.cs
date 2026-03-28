using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public class EffectiveExportPolicyResolverTests
{
    private static RoleSettings MakeRoleSettings(ExportLimitMode mode, int? rows = null) =>
        new() { RoleName = "TestRole", ExportLimitMode = mode, ExportLimitRows = rows };

    [Fact]
    public void Resolve_SuperAdmin_AlwaysUnlimited()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Limited, 1000);

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.SuperAdmin, roleSettings);

        Assert.Equal(ExportLimitMode.Unlimited, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.SuperAdminFixed, policy.Source);
    }

    [Fact]
    public void Resolve_SuperAdmin_IgnoresUserOverride()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Unlimited);
        var user = new ApplicationUser { ExportLimitOverrideMode = ExportLimitMode.Disabled };

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.SuperAdmin, roleSettings, user);

        Assert.Equal(ExportLimitMode.Unlimited, policy.Mode);
        Assert.Equal(EffectivePolicySource.SuperAdminFixed, policy.Source);
    }

    [Fact]
    public void Resolve_NoOverride_UsesRolePolicy()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Limited, 5000);
        var user = new ApplicationUser { ExportLimitOverrideMode = null };

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Internal, roleSettings, user);

        Assert.Equal(ExportLimitMode.Limited, policy.Mode);
        Assert.Equal(5000, policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.Role, policy.Source);
    }

    [Fact]
    public void Resolve_NullUser_UsesRolePolicy()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Unlimited);

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Admin, roleSettings, null);

        Assert.Equal(ExportLimitMode.Unlimited, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.Role, policy.Source);
    }

    [Fact]
    public void Resolve_LimitedOverride_UsesOverride()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Unlimited);
        var user = new ApplicationUser
        {
            ExportLimitOverrideMode = ExportLimitMode.Limited,
            ExportLimitRowsOverride = 200
        };

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Internal, roleSettings, user);

        Assert.Equal(ExportLimitMode.Limited, policy.Mode);
        Assert.Equal(200, policy.Rows);
        Assert.True(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.UserOverride, policy.Source);
    }

    [Fact]
    public void Resolve_UnlimitedOverride_UsesOverride()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Limited, 100);
        var user = new ApplicationUser { ExportLimitOverrideMode = ExportLimitMode.Unlimited };

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Client, roleSettings, user);

        Assert.Equal(ExportLimitMode.Unlimited, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.True(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.UserOverride, policy.Source);
    }

    [Fact]
    public void Resolve_DisabledOverride_UsesOverride()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Unlimited);
        var user = new ApplicationUser { ExportLimitOverrideMode = ExportLimitMode.Disabled };

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Client, roleSettings, user);

        Assert.Equal(ExportLimitMode.Disabled, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.True(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.UserOverride, policy.Source);
    }

    [Fact]
    public void Resolve_RoleDisabled_UsesRolePolicy()
    {
        var roleSettings = MakeRoleSettings(ExportLimitMode.Disabled);

        var policy = EffectiveExportPolicyResolver.Resolve(AppRoles.Client, roleSettings, null);

        Assert.Equal(ExportLimitMode.Disabled, policy.Mode);
        Assert.Null(policy.Rows);
        Assert.False(policy.IsOverridden);
        Assert.Equal(EffectivePolicySource.Role, policy.Source);
    }
}
