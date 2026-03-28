using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public class RoleSettingsValidationTests
{
    [Fact]
    public void ValidateUpdateItem_SuperAdmin_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.SuperAdmin, ExportLimitMode.Unlimited, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
        Assert.Contains("SuperAdmin", error);
    }

    [Fact]
    public void ValidateUpdateItem_InvalidRole_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto("UnknownRole", ExportLimitMode.Unlimited, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_NullMode_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Internal, null, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_Limited_NullRows_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Internal, ExportLimitMode.Limited, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_Limited_ZeroRows_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Internal, ExportLimitMode.Limited, 0);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_Limited_NegativeRows_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Internal, ExportLimitMode.Limited, -1);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_Limited_PositiveRows_ReturnsNull()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Internal, ExportLimitMode.Limited, 1000);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateUpdateItem_Disabled_NullRows_ReturnsNull()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Client, ExportLimitMode.Disabled, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateUpdateItem_Disabled_NonNullRows_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Client, ExportLimitMode.Disabled, 100);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpdateItem_Unlimited_NullRows_ReturnsNull()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Admin, ExportLimitMode.Unlimited, null);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateUpdateItem_Unlimited_NonNullRows_ReturnsError()
    {
        var item = new RoleSettingUpdateItemDto(AppRoles.Admin, ExportLimitMode.Unlimited, 500);

        var error = RoleSettingsValidation.ValidateUpdateItem(item);

        Assert.NotNull(error);
    }
}
