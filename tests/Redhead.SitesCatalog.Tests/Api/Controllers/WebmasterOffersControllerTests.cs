using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class WebmasterOffersControllerTests
{
    [Theory]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Linkbuilder, true)]
    [InlineData(AppRoles.Editor, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.SuperAdmin, true)]
    public async Task GetByDomain_ReturnsOnlyTheResponseAllowedByTheRole(string role, bool fullAccess)
    {
        // Arrange
        var service = new Mock<IWebmasterOffersService>(MockBehavior.Strict);
        object expected;
        if (fullAccess)
        {
            var response = new WebmasterOffersSearchResult { Domain = "example.org", SiteFound = true };
            service.Setup(x => x.GetByDomainAsync("example.org", CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        else
        {
            var response = new WebmasterOffersSummarySearchResult { Domain = "example.org", SiteFound = true };
            service.Setup(x => x.GetSummaryByDomainAsync("example.org", CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        var controller = new WebmasterOffersController(service.Object, NullLogger<WebmasterOffersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(role) }
            }
        };

        // Act
        var result = await controller.GetByDomain("example.org", CancellationToken.None);

        // Assert
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Theory]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersSummaryReadAccess, true)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersReadAccess, false)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersManageAccess, false)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersImportAccess, false)]
    [InlineData(AppRoles.Client, AppPolicies.WebmasterOffersSummaryReadAccess, false)]
    [InlineData(AppRoles.Lite, AppPolicies.WebmasterOffersSummaryReadAccess, false)]
    [InlineData(AppRoles.Admin, AppPolicies.WebmasterOffersExportAccess, true)]
    [InlineData(AppRoles.SuperAdmin, AppPolicies.WebmasterOffersExportAccess, true)]
    [InlineData(AppRoles.Editor, AppPolicies.WebmasterOffersExportAccess, false)]
    [InlineData(AppRoles.Linkbuilder, AppPolicies.WebmasterOffersExportAccess, false)]
    [InlineData(AppRoles.Internal, AppPolicies.WebmasterOffersExportAccess, false)]
    [InlineData(AppRoles.Client, AppPolicies.WebmasterOffersExportAccess, false)]
    [InlineData(AppRoles.Lite, AppPolicies.WebmasterOffersExportAccess, false)]
    public async Task Authorization_EnforcesTheConfiguredPermissionPolicies(string role, string policy, bool expected)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var (permission, policyName) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(policyName, builder => builder.RequireRole(RolePermissionMatrix.GetRolesForPermission(permission)));
            }
        });
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        // Act
        var result = await authorization.AuthorizeAsync(CreatePrincipal(role), null, policy);

        // Assert
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(AppRoles.Internal, false)]
    [InlineData(AppRoles.Linkbuilder, true)]
    [InlineData(AppRoles.Editor, true)]
    [InlineData(AppRoles.Admin, true)]
    [InlineData(AppRoles.SuperAdmin, true)]
    public async Task MultiSearch_SelectsTheResponseAllowedByTheRole(string role, bool fullAccess)
    {
        // Arrange
        var service = new Mock<IWebmasterOffersService>(MockBehavior.Strict);
        const string query = "first.example second.example";
        object expected;
        if (fullAccess)
        {
            var response = new WebmasterOffersMultiSearchResult<WebmasterOfferDto>();
            service.Setup(item => item.MultiSearchAsync(query, CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        else
        {
            var response = new WebmasterOffersMultiSearchResult<WebmasterOfferSummaryDto>();
            service.Setup(item => item.MultiSearchSummaryAsync(query, CancellationToken.None)).ReturnsAsync(response);
            expected = response;
        }
        var controller = CreateController(service.Object, role);

        // Act
        var result = await controller.MultiSearch(new WebmasterOffersMultiSearchRequest { QueryText = query }, CancellationToken.None);

        // Assert
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Export_UsesTheWholeSearchAndRequiresTheIndependentExportPermission()
    {
        // Arrange
        var service = new Mock<IWebmasterOffersService>(MockBehavior.Strict);
        const string query = "example.org";
        service.Setup(item => item.MultiSearchAsync(query, CancellationToken.None))
            .ReturnsAsync(new WebmasterOffersMultiSearchResult<WebmasterOfferDto>
            {
                Results = [new WebmasterOffersDomainResult<WebmasterOfferDto>
                {
                    Domain = query, Offers = Enumerable.Range(0, 75).Select(_ => new WebmasterOfferDto()).ToList()
                }]
            });
        var controller = CreateController(service.Object, AppRoles.Admin);

        // Act
        var result = await controller.Export(new WebmasterOffersMultiSearchRequest { QueryText = query }, CancellationToken.None);

        // Assert
        var file = Assert.IsType<FileStreamResult>(result);
        using var stream = file.FileStream;
        Assert.Equal("webmaster-offers.xlsx", file.FileDownloadName);
        Assert.Equal(75, XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers").Count);
        var policy = Assert.Single(typeof(WebmasterOffersController).GetMethod(nameof(WebmasterOffersController.Export))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(AppPolicies.WebmasterOffersExportAccess, policy.Policy);
    }

    private static WebmasterOffersController CreateController(IWebmasterOffersService service, string role)
        => new(service, NullLogger<WebmasterOffersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(role) }
            }
        };

    private static ClaimsPrincipal CreatePrincipal(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
}
