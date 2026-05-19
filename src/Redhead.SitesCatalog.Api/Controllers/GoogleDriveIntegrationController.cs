using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Options;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Infrastructure.Exceptions;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/integrations/google-drive")]
public class GoogleDriveIntegrationController : ControllerBase
{
    private readonly IGoogleDriveIntegrationService _googleDriveIntegrationService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<GoogleDriveIntegrationController> _logger;

    public GoogleDriveIntegrationController(
        IGoogleDriveIntegrationService googleDriveIntegrationService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<GoogleDriveIntegrationController> logger)
    {
        _googleDriveIntegrationService = googleDriveIntegrationService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<GoogleDriveStatusResponse>> Status(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        return Ok(await _googleDriveIntegrationService.GetStatusAsync(userId, cancellationToken));
    }

    [HttpPost("connect/start")]
    public ActionResult<GoogleDriveConnectStartResponse> StartConnection()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(new GoogleDriveConnectStartResponse(
                _googleDriveIntegrationService.CreateAuthorizationUrl(userId)));
        }
        catch (GoogleDriveConfigurationException ex)
        {
            return Problem(
                title: "Google Drive integration is not configured.",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null || !string.IsNullOrWhiteSpace(error))
        {
            return RedirectToSites("error");
        }

        try
        {
            await _googleDriveIntegrationService.CompleteConnectionAsync(
                userId,
                code,
                state,
                cancellationToken);

            return RedirectToSites("connected");
        }
        catch (Exception ex) when (ex is GoogleDriveConfigurationException or GoogleDriveIntegrationException)
        {
            _logger.LogWarning(ex, "Google Drive connection callback failed for user {UserId}", userId);
            return RedirectToSites("error");
        }
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _googleDriveIntegrationService.DisconnectAsync(userId, cancellationToken);
        return NoContent();
    }

    private string? GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private RedirectResult RedirectToSites(string googleDriveStatus)
    {
        var path = $"/sites?googleDrive={Uri.EscapeDataString(googleDriveStatus)}";
        if (string.IsNullOrWhiteSpace(_frontendOptions.BaseUrl))
        {
            return Redirect(path);
        }

        return Redirect($"{_frontendOptions.BaseUrl.TrimEnd('/')}{path}");
    }
}
