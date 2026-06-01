using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.AccountSetup;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAccountSetupService _accountSetupService;
    private readonly IEffectiveExportPolicyService _effectiveExportPolicyService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAccountSetupService accountSetupService,
        IEffectiveExportPolicyService effectiveExportPolicyService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _accountSetupService = accountSetupService;
        _effectiveExportPolicyService = effectiveExportPolicyService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for {Email}", request.Email);

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found {Email}", request.Email);
            return Unauthorized(new MessageResponse("Invalid email or password"));
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: Account disabled {Email}", request.Email);
            return Unauthorized(new MessageResponse("Your account has been disabled. Please contact an administrator."));
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            request.Password,
            isPersistent: request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Successful login for {Email}", user.Email);
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(ToLoginResponse(user, roles));
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login failed: Account locked out {Email}", request.Email);
            return Unauthorized(new MessageResponse("Your account has been locked due to multiple failed login attempts. Please try again later."));
        }

        _logger.LogWarning("Login failed: Invalid credentials {Email}", request.Email);
        return Unauthorized(new MessageResponse("Invalid email or password"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<MessageResponse>> Logout()
    {
        var userEmail = User.Identity?.Name;
        _logger.LogInformation("User logged out: {Email}", userEmail);
        
        await _signInManager.SignOutAsync();
        return Ok(new MessageResponse("Logged out successfully"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfoResponse>> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            _logger.LogWarning("GetCurrentUser failed: User not found in context");
            return Unauthorized();
        }

        // Check if user is still active
        if (!user.IsActive)
        {
            _logger.LogWarning("GetCurrentUser: Account disabled, signing out {Email}", user.Email);
            await _signInManager.SignOutAsync();
            return Unauthorized(new MessageResponse("Your account has been disabled."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var limits = await _effectiveExportPolicyService.GetEffectivePolicyAsync(user, role);

        return Ok(new UserInfoResponse(
            user.Id,
            user.Email!,
            user.MustChangePassword,
            !user.HasCompleteProfile,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.IsActive,
            roles,
            limits.Mode == ExportLimitMode.Disabled));
    }

    [HttpPost("complete-account-setup")]
    [Authorize]
    public async Task<ActionResult<CompleteAccountSetupResponse>> CompleteAccountSetup(
        [FromBody] CompleteAccountSetupRequest request)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            _logger.LogWarning("CompleteAccountSetup failed: User not found in context");
            return Unauthorized();
        }

        var result = await _accountSetupService.CompleteAsync(user, request);
        return ToActionResult(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<MessageResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            _logger.LogWarning("ChangePassword failed: User not found in context");
            return Unauthorized();
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (result.Succeeded)
        {
            _logger.LogInformation("Password changed successfully for {Email}", user.Email);
            
            // Clear the MustChangePassword flag
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            return Ok(new MessageResponse("Password changed successfully"));
        }

        _logger.LogWarning("Password change failed for {Email}: {Errors}", 
            user.Email, 
            string.Join(", ", result.Errors.Select(e => e.Description)));

        return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    }

    private static LoginResponse ToLoginResponse(ApplicationUser user, IList<string> roles)
    {
        return new LoginResponse(
            user.Email!,
            user.MustChangePassword,
            !user.HasCompleteProfile,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            roles);
    }

    private ActionResult<CompleteAccountSetupResponse> ToActionResult(
        AccountSetupCompletionResult result)
        => result.Status switch
        {
            AccountSetupCompletionStatus.Success => Ok(result.Response),
            AccountSetupCompletionStatus.ValidationFailed => BadRequest(
                new ValidationProblemDetails(result.ValidationErrors.ToDictionary(
                    error => error.Key,
                    error => error.Value))),
            AccountSetupCompletionStatus.PasswordChangeFailed => BadRequest(new { errors = result.Errors }),
            AccountSetupCompletionStatus.UserUpdateFailed => BadRequest(
                new MessageResponse(string.Join(" ", result.Errors))),
            _ => BadRequest(new MessageResponse("Account setup could not be completed."))
        };
}
