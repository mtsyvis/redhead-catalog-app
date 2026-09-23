using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ApplicationSettingsControllerTests
{
    [Fact]
    public void GetLimits_ReturnsRuntimeConfigurationAndBuiltInLimits()
    {
        // Arrange
        var options = Options.Create(new ClientCatalogOptions
        {
            RequestsPerMinute = 73,
            UniqueSitesPerFiveMinutes = 2_345,
            AlertUniqueSitesPerHour = 6_789
        });
        var sut = new ApplicationSettingsController(options);

        // Act
        var result = sut.GetLimits();

        // Assert
        var response = Assert.IsType<ApplicationSettingsLimitsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(ClientCatalogLimits.DefaultSelectionLimit, response.DefaultClientSelectionLimit);
        Assert.Equal(ClientCatalogLimits.MaxSelectionLimit, response.MaxClientSelectionLimit);
        Assert.Equal(73, response.ClientRequestsPerMinute);
        Assert.Equal(2_345, response.ClientUniqueSitesPerFiveMinutes);
        Assert.Equal(6_789, response.ClientAlertUniqueSitesPerHour);
        Assert.Equal(MultiSearchConstants.MaxInputs, response.GlobalMultiSearchMaxInputs);
        Assert.Equal(StopListConstants.MaxStopListDomains, response.StopListMaxDomains);
        Assert.Equal(LiteMultiSearchConstants.MaxDomainsPerRequest, response.LiteMultiSearchMaxDomainsPerRequest);
        Assert.Equal(LiteMultiSearchConstants.MonthlyDomainLimit, response.LiteMonthlyDomainLimit);
        Assert.Equal(TableViewConstants.CustomViewsPerUserTableLimit, response.CustomTableViewsPerUserTable);
        Assert.Equal(SavedFilterSetConstants.FilterSetsPerUserTableLimit, response.SavedFilterSetsPerUserTable);
    }

    [Fact]
    public void Controller_RequiresRoleSettingsReadPermission()
    {
        // Arrange
        var controllerType = typeof(ApplicationSettingsController);

        // Act
        var attribute = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        // Assert
        Assert.Equal(AppPolicies.RoleSettingsReadAccess, attribute.Policy);
    }
}
