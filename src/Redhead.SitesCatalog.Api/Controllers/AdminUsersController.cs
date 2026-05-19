using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Security;
using Redhead.SitesCatalog.Api.Validation;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAdminUsersListService _usersListService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        IAdminUsersListService usersListService,
        ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
        _usersListService = usersListService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult<CreateUserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!AppRoles.All.Contains(request.Role))
        {
            return BadRequest(new MessageResponse("Invalid role."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : Array.Empty<string>();
        if (!AdminUsersAuthorization.CanCreateRole(currentRoles, request.Role))
        {
            return Forbid();
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return BadRequest(new MessageResponse("A user with this email already exists."));
        }

        var temporaryPassword = PasswordGenerator.Generate();
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
        };

        var result = await _userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return BadRequest(new MessageResponse(errors));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            var errors = string.Join(" ", roleResult.Errors.Select(e => e.Description));
            return BadRequest(new MessageResponse(errors));
        }

        _logger.LogInformation("User created: {Email}, role: {Role}", user.Email, request.Role);

        return Ok(new CreateUserResponse(user.Id, user.Email!, request.Role, temporaryPassword));
    }

    [HttpGet]
    public async Task<ActionResult<UserListResponse>> ListUsers(
        [FromQuery] UserListRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = AdminUsersListRequestValidation.Validate(request);
        if (validationError != null)
        {
            return BadRequest(new MessageResponse(validationError));
        }

        var result = await _usersListService.ListUsersAsync(ToQuery(request), cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpPost("{id}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : Array.Empty<string>();
        var targetRoles = await _userManager.GetRolesAsync(target);
        if (!AdminUsersAuthorization.CanModifyUser(currentRoles, targetRoles))
        {
            return Forbid();
        }

        var temporaryPassword = PasswordGenerator.Generate();
        var token = await _userManager.GeneratePasswordResetTokenAsync(target);
        var result = await _userManager.ResetPasswordAsync(target, token, temporaryPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return BadRequest(new MessageResponse(errors));
        }

        target.MustChangePassword = true;
        await _userManager.UpdateAsync(target);

        _logger.LogInformation("Password reset for user: {Email}", target.Email);

        return Ok(new ResetPasswordResponse(temporaryPassword));
    }

    [HttpPost("{id}/disable")]
    public async Task<ActionResult<MessageResponse>> DisableUser(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : Array.Empty<string>();
        var targetRoles = await _userManager.GetRolesAsync(target);
        if (!AdminUsersAuthorization.CanModifyUser(currentRoles, targetRoles))
        {
            return Forbid();
        }

        if (!target.IsActive)
        {
            return BadRequest(new MessageResponse("User is already disabled."));
        }

        target.IsActive = false;
        await _userManager.UpdateAsync(target);

        _logger.LogInformation("User disabled: {Email}", target.Email);

        return Ok(new MessageResponse("User has been disabled."));
    }

    [HttpPut("{id}/export-limit")]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult> UpdateUserExportLimit(
        string id,
        [FromBody] UpdateUserExportLimitRequest request,
        CancellationToken cancellationToken)
    {
        var targetUser = await _userManager.FindByIdAsync(id);
        if (targetUser == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        var targetRoles = await _userManager.GetRolesAsync(targetUser);
        var targetRole = targetRoles.FirstOrDefault() ?? string.Empty;

        var roleError = UserExportLimitValidation.ValidateTargetRole(targetRole);
        if (roleError != null)
        {
            return BadRequest(new MessageResponse(roleError));
        }

        var overrideError = UserExportLimitValidation.ValidateOverride(request);
        if (overrideError != null)
        {
            return BadRequest(new MessageResponse(overrideError));
        }

        targetUser.ExportLimitOverrideMode = request.OverrideMode;
        targetUser.ExportLimitRowsOverride = request.OverrideMode == ExportLimitMode.Limited
            ? request.OverrideRows
            : null;

        await _userManager.UpdateAsync(targetUser);

        _logger.LogInformation(
            "Export limit override updated for user: {Email}, mode: {Mode}",
            targetUser.Email, request.OverrideMode);

        return NoContent();
    }

    private static AdminUsersListQuery ToQuery(UserListRequest request)
    {
        return new AdminUsersListQuery
        {
            UserType = AdminUsersListRequestValidation.NormalizeUserType(request.UserType),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static UserListResponse ToResponse(AdminUsersListResult result)
    {
        return new UserListResponse(
            Items: result.Items.Select(ToResponseItem).ToList(),
            Page: result.Page,
            PageSize: result.PageSize,
            TotalCount: result.TotalCount,
            TotalPages: result.TotalPages);
    }

    private static UserListItem ToResponseItem(AdminUserListItemDto item)
    {
        return new UserListItem(
            Id: item.Id,
            Email: item.Email,
            Role: item.Role,
            IsActive: item.IsActive,
            ExportLimitOverrideMode: item.ExportLimitOverrideMode,
            ExportLimitRowsOverride: item.ExportLimitRowsOverride,
            EffectiveExportLimitMode: item.EffectiveExportLimitMode,
            EffectiveExportLimitRows: item.EffectiveExportLimitRows,
            IsExportLimitOverridden: item.IsExportLimitOverridden,
            IsExportLimitEditable: item.IsExportLimitEditable);
    }
}
