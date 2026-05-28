using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Export;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class ExportControllerTests
{
    [Fact]
    public async Task ExportSitesToGoogleDrive_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = await controller.ExportSitesToGoogleDrive(new GoogleDriveExportRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ExportSitesToGoogleDrive_WhenNoConnection_ReturnsClearApiError()
    {
        var googleDriveExportService = new Mock<IGoogleDriveExportService>();
        googleDriveExportService
            .Setup(service => service.ExportSitesAsync(
                It.IsAny<SitesQuery>(),
                "user-1",
                "user@example.com",
                AppRoles.Admin,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(GoogleDriveExportException.NotConnected());
        var controller = CreateController(googleDriveExportService: googleDriveExportService.Object);
        SetAuthenticatedUser(controller);

        var result = await controller.ExportSitesToGoogleDrive(new GoogleDriveExportRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var error = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal(GoogleDriveExportException.NotConnectedErrorCode, error.Error);
    }

    [Fact]
    public async Task ExportSitesToGoogleDrive_WithMultiSearchPayload_UsesMultiSearchExport()
    {
        var googleDriveExportService = new Mock<IGoogleDriveExportService>();
        googleDriveExportService
            .Setup(service => service.ExportMultiSearchAsync(
                "first.com second.com",
                It.IsAny<SitesQuery>(),
                "user-1",
                "user@example.com",
                AppRoles.Admin,
                It.Is<IReadOnlyList<string>>(keys => keys.SequenceEqual(new[] { "domain", "traffic" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveExportResponse(
                "file-1",
                ExportConstants.SitesFileName,
                null,
                2,
                false,
                DateTime.UtcNow,
                "Google Drive / Redhead Catalog Exports"));
        var controller = CreateController(googleDriveExportService: googleDriveExportService.Object);
        SetAuthenticatedUser(controller);
        var request = new GoogleDriveExportRequest
        {
            SearchText = "first.com second.com",
            Filters = new SitesQueryRequest
            {
                SortBy = "domain",
                SortDir = "asc"
            },
            VisibleColumnKeys = ["domain", "traffic"]
        };

        var result = await controller.ExportSitesToGoogleDrive(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GoogleDriveExportResponse>(ok.Value);
        Assert.Equal(2, response.RowsExported);
        googleDriveExportService.Verify(
            service => service.ExportSitesAsync(
                It.IsAny<SitesQuery>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ExportController CreateController(
        IExportService? exportService = null,
        IGoogleDriveExportService? googleDriveExportService = null)
        => new(
            exportService ?? Mock.Of<IExportService>(),
            googleDriveExportService ?? Mock.Of<IGoogleDriveExportService>());

    private static void SetAuthenticatedUser(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    new Claim(ClaimTypes.Email, "user@example.com"),
                    new Claim(ClaimTypes.Role, AppRoles.Admin)
                ], "Test"))
            }
        };
    }
}
