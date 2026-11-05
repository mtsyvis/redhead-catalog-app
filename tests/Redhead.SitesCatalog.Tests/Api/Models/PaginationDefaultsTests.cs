using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Api.Validation;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Models;

public sealed class PaginationDefaultsTests
{
    [Fact]
    public void PaginatedRequests_DefaultToTheSamePageSize()
    {
        // Arrange
        var requests = new object[] { new SitesQueryRequest(), new UserListRequest(), new ExportActivityAnalyticsRequest(),
            new MissingDomainsAnalyticsRequest(), new PricingQuery() };

        // Act
        var defaults = requests.Select(request => (int)request.GetType().GetProperty("PageSize")!.GetValue(request)!);

        // Assert
        Assert.All(defaults, size => Assert.Equal(PaginationDefaults.DefaultPageSize, size));
    }

    [Fact]
    public void PaginatedEndpoints_DefaultToTheSamePageAndPageSize()
    {
        // Arrange
        var endpoints = typeof(PricingController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.Name is "page" or "pageSize" && parameter.HasDefaultValue)
            .ToList();

        // Act
        var defaults = endpoints.Select(parameter => new { parameter.Name, Value = (int)parameter.DefaultValue! }).ToList();

        // Assert
        Assert.NotEmpty(defaults);
        Assert.All(defaults, parameter => Assert.Equal(parameter.Name == "page" ? PaginationDefaults.DefaultPage : PaginationDefaults.DefaultPageSize, parameter.Value));
    }

    public static IEnumerable<object[]> StandardPageSizes => PaginationDefaults.PageSizes.Select(size => new object[] { size });

    [Theory]
    [MemberData(nameof(StandardPageSizes))]
    public void StandardPageSizes_AreAcceptedByUsersAndAnalyticsApis(int pageSize)
    {
        // Arrange
        var users = new UserListRequest { UserType = "all", PageSize = pageSize };
        var exports = new ExportActivityAnalyticsRequest { PageSize = pageSize };
        var missing = new MissingDomainsAnalyticsRequest { PageSize = pageSize };
        var now = DateTimeOffset.UtcNow;

        // Act
        var usersError = AdminUsersListRequestValidation.Validate(users);
        var exportsError = AnalyticsRequestMapper.ToExportActivityQuery(exports, now).Error;
        var missingError = AnalyticsRequestMapper.ToMissingDomainsQuery(missing, now).Error;

        // Assert
        Assert.Null(usersError);
        Assert.Null(exportsError);
        Assert.Null(missingError);
    }
}
