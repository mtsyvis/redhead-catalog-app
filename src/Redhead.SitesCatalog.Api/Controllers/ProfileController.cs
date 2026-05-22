using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGoogleDriveIntegrationService _googleDriveIntegrationService;
    private readonly IEffectiveExportPolicyService _effectiveExportPolicyService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IGoogleDriveIntegrationService googleDriveIntegrationService,
        IEffectiveExportPolicyService effectiveExportPolicyService,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _googleDriveIntegrationService = googleDriveIntegrationService;
        _effectiveExportPolicyService = effectiveExportPolicyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<CurrentUserProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var googleDrive = await _googleDriveIntegrationService.GetStatusAsync(user.Id, cancellationToken);
        var limits = await _effectiveExportPolicyService.GetEffectivePolicyAsync(user, role, cancellationToken);

        return Ok(ToResponse(user, role, googleDrive, limits));
    }

    [HttpPut]
    public async Task<ActionResult<CurrentUserProfileResponse>> UpdateProfile(
        [FromBody] UpdateCurrentUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var validation = UserProfileNameValidator.Validate(request.FirstName, request.LastName);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.ToDictionary(error => error.Key, error => error.Value)));
        }

        user.FirstName = validation.FirstName;
        user.LastName = validation.LastName;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(" ", updateResult.Errors.Select(error => error.Description));
            return BadRequest(new MessageResponse(errors));
        }

        _logger.LogInformation("Profile updated for user: {Email}", user.Email);

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var googleDrive = await _googleDriveIntegrationService.GetStatusAsync(user.Id, cancellationToken);
        var limits = await _effectiveExportPolicyService.GetEffectivePolicyAsync(user, role, cancellationToken);

        return Ok(ToResponse(user, role, googleDrive, limits));
    }

    private static CurrentUserProfileResponse ToResponse(
        ApplicationUser user,
        string role,
        GoogleDriveStatusResponse googleDrive,
        EffectiveExportPolicy limits)
    {
        return new CurrentUserProfileResponse(
            user.Email ?? string.Empty,
            role,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            !user.HasCompleteProfile,
            googleDrive,
            ToLimitsResponse(limits));
    }

    private static CurrentUserProfileLimitsResponse ToLimitsResponse(EffectiveExportPolicy policy)
        => new(
            policy.Mode,
            policy.Mode == ExportLimitMode.Limited ? policy.Rows : null,
            policy.Mode == ExportLimitMode.Unlimited);
}
