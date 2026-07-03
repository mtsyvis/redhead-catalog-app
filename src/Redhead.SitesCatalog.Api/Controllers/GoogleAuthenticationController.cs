using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Authentication;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth/google")]
public sealed class GoogleAuthenticationController : ControllerBase
{
    public const string EmailVerifiedClaimType = "urn:google:email_verified";
    public const string PictureClaimType = "urn:google:picture";
    private const string ReturnUrlProperty = "returnUrl";

    private readonly IGoogleAccountAuthenticationService _googleAccountAuthenticationService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly GoogleAuthenticationOptions _googleOptions;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<GoogleAuthenticationController> _logger;

    public GoogleAuthenticationController(
        IGoogleAccountAuthenticationService googleAccountAuthenticationService,
        SignInManager<ApplicationUser> signInManager,
        IOptions<GoogleAuthenticationOptions> googleOptions,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<GoogleAuthenticationController> logger)
    {
        _googleAccountAuthenticationService = googleAccountAuthenticationService;
        _signInManager = signInManager;
        _googleOptions = googleOptions.Value;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    public ActionResult<GoogleAuthenticationStatusResponse> Status()
        => Ok(new GoogleAuthenticationStatusResponse(_googleOptions.Enabled));

    [HttpGet("start")]
    public IActionResult Start([FromQuery] string? returnUrl = null)
    {
        if (!_googleOptions.Enabled)
        {
            return RedirectToLogin("unavailable");
        }

        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            ExternalLoginProviders.Google,
            Url.Action(nameof(Callback), "GoogleAuthentication"));
        properties.Items[ReturnUrlProperty] = NormalizeReturnUrl(returnUrl);

        return Challenge(properties, ExternalLoginProviders.Google);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        if (!_googleOptions.Enabled)
        {
            return RedirectToLogin("unavailable");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null ||
            !string.Equals(info.LoginProvider, ExternalLoginProviders.Google, StringComparison.Ordinal))
        {
            return RedirectToLogin("invalid");
        }

        var identity = new GoogleIdentity(
            info.ProviderKey,
            info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            bool.TryParse(info.Principal.FindFirstValue(EmailVerifiedClaimType), out var verified) && verified,
            info.Principal.FindFirstValue(ClaimTypes.Name),
            info.Principal.FindFirstValue(PictureClaimType));

        var result = await _googleAccountAuthenticationService.AuthenticateAsync(identity, cancellationToken);
        if (result.Status != GoogleAccountAuthenticationStatus.Success || result.User == null)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToLogin(ToErrorCode(result.Status));
        }

        await _signInManager.SignInAsync(
            result.User,
            isPersistent: false,
            authenticationMethod: ExternalLoginProviders.Google);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        _logger.LogInformation(
            "Google authentication succeeded. UserId={UserId}, Created={Created}",
            result.User.Id,
            result.Created);

        string? returnUrl = null;
        info.AuthenticationProperties?.Items.TryGetValue(ReturnUrlProperty, out returnUrl);
        return RedirectToFrontend(NormalizeReturnUrl(returnUrl));
    }

    [HttpGet("failure")]
    public IActionResult Failure()
        => RedirectToLogin("cancelled");

    internal static string NormalizeReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) &&
           returnUrl.StartsWith("/", StringComparison.Ordinal) &&
           !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
           !returnUrl.StartsWith("/\\", StringComparison.Ordinal) &&
           !returnUrl.Any(char.IsControl)
            ? returnUrl
            : "/sites";

    private RedirectResult RedirectToLogin(string errorCode)
        => RedirectToFrontend($"/login?googleAuth={Uri.EscapeDataString(errorCode)}");

    private RedirectResult RedirectToFrontend(string path)
    {
        if (string.IsNullOrWhiteSpace(_frontendOptions.BaseUrl))
        {
            return Redirect(path);
        }

        return Redirect($"{_frontendOptions.BaseUrl.TrimEnd('/')}{path}");
    }

    private static string ToErrorCode(GoogleAccountAuthenticationStatus status)
        => status switch
        {
            GoogleAccountAuthenticationStatus.InvalidIdentity => "invalid",
            GoogleAccountAuthenticationStatus.EmailConflict => "email-conflict",
            GoogleAccountAuthenticationStatus.Disabled => "disabled",
            _ => "failed"
        };
}
