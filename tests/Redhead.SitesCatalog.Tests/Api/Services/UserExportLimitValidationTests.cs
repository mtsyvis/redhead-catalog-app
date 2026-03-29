using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Services;

public class UserExportLimitValidationTests
{
    // ValidateTargetRole

    [Fact]
    public void ValidateTargetRole_SuperAdmin_ReturnsError()
    {
        var error = UserExportLimitValidation.ValidateTargetRole(AppRoles.SuperAdmin);

        Assert.NotNull(error);
        Assert.Contains("SuperAdmin", error);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Internal)]
    [InlineData(AppRoles.Client)]
    public void ValidateTargetRole_NonSuperAdmin_ReturnsNull(string role)
    {
        var error = UserExportLimitValidation.ValidateTargetRole(role);

        Assert.Null(error);
    }

    // ValidateOverride: clear override (null/null)

    [Fact]
    public void ValidateOverride_NullMode_NullRows_ReturnsNull()
    {
        var request = new UpdateUserExportLimitRequest(null, null);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateOverride_NullMode_NonNullRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(null, 500);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }

    // ValidateOverride: Limited

    [Fact]
    public void ValidateOverride_Limited_PositiveRows_ReturnsNull()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Limited, 1000);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateOverride_Limited_NullRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Limited, null);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateOverride_Limited_ZeroRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Limited, 0);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateOverride_Limited_NegativeRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Limited, -1);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }

    // ValidateOverride: Disabled

    [Fact]
    public void ValidateOverride_Disabled_NullRows_ReturnsNull()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Disabled, null);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateOverride_Disabled_NonNullRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Disabled, 100);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }

    // ValidateOverride: Unlimited

    [Fact]
    public void ValidateOverride_Unlimited_NullRows_ReturnsNull()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Unlimited, null);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateOverride_Unlimited_NonNullRows_ReturnsError()
    {
        var request = new UpdateUserExportLimitRequest(ExportLimitMode.Unlimited, 500);

        var error = UserExportLimitValidation.ValidateOverride(request);

        Assert.NotNull(error);
    }
}
