using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Security;
using Redhead.SitesCatalog.Api.Validation;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Validation;
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

        var noteValidation = SuperAdminNoteValidator.Validate(request.SuperAdminNote);
        if (!noteValidation.IsValid)
        {
            return BadRequest(new MessageResponse(noteValidation.Error!));
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            var message = existing.IsActive
                ? "A user with this email already exists."
                : "A disabled user with this email already exists. Reactivate that user instead.";
            return BadRequest(new MessageResponse(message));
        }

        var temporaryPassword = PasswordGenerator.Generate();
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            SuperAdminNote = currentRoles.Contains(AppRoles.SuperAdmin) ? noteValidation.Value : null,
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

        var includeSuperAdminNote = await IsCurrentUserSuperAdminAsync();
        var result = await _usersListService.ListUsersAsync(ToQuery(request), cancellationToken);
        return Ok(ToResponse(result, includeSuperAdminNote));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminUserDetailsResponse>> GetUser(
        string id,
        CancellationToken cancellationToken)
    {
        var user = await _usersListService.GetUserDetailsAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        var includeSuperAdminNote = await IsCurrentUserSuperAdminAsync();
        return Ok(ToResponse(user, includeSuperAdminNote));
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

        if (currentUser?.Id == target.Id)
        {
            return BadRequest(new MessageResponse("You cannot disable your own account."));
        }

        if (!target.IsActive)
        {
            return BadRequest(new MessageResponse("User is already disabled."));
        }

        if (targetRoles.Contains(AppRoles.SuperAdmin) && await IsLastActiveSuperAdminAsync(target))
        {
            return BadRequest(new MessageResponse("Cannot disable the last active SuperAdmin."));
        }

        target.IsActive = false;
        await _userManager.UpdateAsync(target);

        _logger.LogInformation("User disabled: {Email}", target.Email);

        return Ok(new MessageResponse("User has been disabled."));
    }

    [HttpPut("{id}/role")]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult> UpdateUserRole(string id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!AppRoles.All.Contains(request.Role))
        {
            return BadRequest(new MessageResponse("Invalid role."));
        }

        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Forbid();
        }

        var currentRoles = await _userManager.GetRolesAsync(currentUser);
        if (!currentRoles.Contains(AppRoles.SuperAdmin))
        {
            return Forbid();
        }

        if (currentUser.Id == target.Id)
        {
            return BadRequest(new MessageResponse("You cannot change your own role."));
        }

        if (!target.IsActive)
        {
            return BadRequest(new MessageResponse("Cannot change roles for disabled users. Reactivate the user instead."));
        }

        var targetRoles = await _userManager.GetRolesAsync(target);
        var currentRole = targetRoles.FirstOrDefault() ?? string.Empty;
        if (!CanChangeProtectedRole(currentRole, request.Role))
        {
            return BadRequest(new MessageResponse("SuperAdmin is a protected role and cannot be changed here."));
        }

        if (string.Equals(currentRole, request.Role, StringComparison.Ordinal))
        {
            return NoContent();
        }

        var roleResult = await ReplaceUserRoleAsync(target, targetRoles, request.Role);
        if (roleResult != null)
        {
            return BadRequest(new MessageResponse(roleResult));
        }

        var updateResult = await _userManager.UpdateAsync(target);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new MessageResponse(FormatIdentityErrors(updateResult)));
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(target);
        if (!stampResult.Succeeded)
        {
            return BadRequest(new MessageResponse(FormatIdentityErrors(stampResult)));
        }

        _logger.LogInformation(
            "User role updated: {Email}, old role: {OldRole}, new role: {NewRole}",
            target.Email, currentRole, request.Role);

        return NoContent();
    }

    [HttpPost("{id}/reactivate")]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult<ReactivateUserResponse>> ReactivateUser(
        string id,
        [FromBody] ReactivateUserRequest request)
    {
        if (!AppRoles.All.Contains(request.Role))
        {
            return BadRequest(new MessageResponse("Invalid role."));
        }

        if (!await IsCurrentUserSuperAdminAsync())
        {
            return Forbid();
        }

        var target = await _userManager.FindByIdAsync(id);
        if (target == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        if (target.IsActive)
        {
            return BadRequest(new MessageResponse("User is already active."));
        }

        var targetRoles = await _userManager.GetRolesAsync(target);
        var currentRole = targetRoles.FirstOrDefault() ?? string.Empty;
        if (!CanReactivateAsRole(currentRole, request.Role))
        {
            return BadRequest(new MessageResponse("Invalid reactivation role for this user."));
        }

        if (!string.IsNullOrWhiteSpace(target.Email))
        {
            var existing = await _userManager.FindByEmailAsync(target.Email);
            if (existing != null && existing.Id != target.Id && existing.IsActive)
            {
                return BadRequest(new MessageResponse("Another active user already uses this email."));
            }
        }

        var temporaryPassword = PasswordGenerator.Generate();
        var token = await _userManager.GeneratePasswordResetTokenAsync(target);
        var passwordResult = await _userManager.ResetPasswordAsync(target, token, temporaryPassword);
        if (!passwordResult.Succeeded)
        {
            return BadRequest(new MessageResponse(FormatIdentityErrors(passwordResult)));
        }

        if (!string.Equals(currentRole, request.Role, StringComparison.Ordinal))
        {
            var roleResult = await ReplaceUserRoleAsync(target, targetRoles, request.Role);
            if (roleResult != null)
            {
                return BadRequest(new MessageResponse(roleResult));
            }
        }

        target.IsActive = true;
        target.MustChangePassword = true;

        var updateResult = await _userManager.UpdateAsync(target);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new MessageResponse(FormatIdentityErrors(updateResult)));
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(target);
        if (!stampResult.Succeeded)
        {
            return BadRequest(new MessageResponse(FormatIdentityErrors(stampResult)));
        }

        _logger.LogInformation(
            "User reactivated: {Email}, role: {Role}",
            target.Email, request.Role);

        return Ok(new ReactivateUserResponse(temporaryPassword));
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

    [HttpPut("{id}/super-admin-note")]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult> UpdateUserSuperAdminNote(
        string id,
        [FromBody] UpdateUserSuperAdminNoteRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (!await IsCurrentUserSuperAdminAsync())
        {
            return Forbid();
        }

        var validation = SuperAdminNoteValidator.Validate(request.SuperAdminNote);
        if (!validation.IsValid)
        {
            return BadRequest(new MessageResponse(validation.Error!));
        }

        var targetUser = await _userManager.FindByIdAsync(id);
        if (targetUser == null)
        {
            return NotFound(new MessageResponse("User not found."));
        }

        targetUser.SuperAdminNote = validation.Value;
        await _userManager.UpdateAsync(targetUser);

        _logger.LogInformation("Super Admin note updated for user: {Email}", targetUser.Email);

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

    private async Task<bool> IsCurrentUserSuperAdminAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var currentRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : Array.Empty<string>();
        return currentRoles.Contains(AppRoles.SuperAdmin);
    }

    private static bool CanChangeProtectedRole(string currentRole, string requestedRole)
    {
        return AppRoles.NonSuperAdmin.Contains(currentRole)
            && AppRoles.NonSuperAdmin.Contains(requestedRole);
    }

    private static bool CanReactivateAsRole(string currentRole, string requestedRole)
    {
        if (string.Equals(currentRole, AppRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return string.Equals(requestedRole, AppRoles.SuperAdmin, StringComparison.Ordinal);
        }

        return AppRoles.NonSuperAdmin.Contains(currentRole)
            && AppRoles.NonSuperAdmin.Contains(requestedRole);
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(ApplicationUser target)
    {
        var superAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        return superAdmins.Count(user => user.IsActive) <= 1 && target.IsActive;
    }

    private async Task<string?> ReplaceUserRoleAsync(
        ApplicationUser user,
        IList<string> currentRoles,
        string requestedRole)
    {
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return FormatIdentityErrors(removeResult);
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, requestedRole);
        if (!addResult.Succeeded)
        {
            return FormatIdentityErrors(addResult);
        }

        return null;
    }

    private static string FormatIdentityErrors(IdentityResult result)
    {
        return string.Join(" ", result.Errors.Select(e => e.Description));
    }

    private static object ToResponse(AdminUsersListResult result, bool includeSuperAdminNote)
    {
        if (includeSuperAdminNote)
        {
            return new SuperAdminUserListResponse(
                Items: result.Items.Select(ToSuperAdminResponseItem).ToList(),
                Page: result.Page,
                PageSize: result.PageSize,
                TotalCount: result.TotalCount,
                TotalPages: result.TotalPages);
        }

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
            FirstName: item.FirstName,
            LastName: item.LastName,
            DisplayName: item.DisplayName,
            MustCompleteProfile: item.MustCompleteProfile,
            Role: item.Role,
            IsActive: item.IsActive,
            ExportLimitOverrideMode: item.ExportLimitOverrideMode,
            ExportLimitRowsOverride: item.ExportLimitRowsOverride,
            EffectiveExportLimitMode: item.EffectiveExportLimitMode,
            EffectiveExportLimitRows: item.EffectiveExportLimitRows,
            IsExportLimitOverridden: item.IsExportLimitOverridden,
            IsExportLimitEditable: item.IsExportLimitEditable);
    }

    private static SuperAdminUserListItem ToSuperAdminResponseItem(AdminUserListItemDto item)
    {
        return new SuperAdminUserListItem(
            Id: item.Id,
            Email: item.Email,
            FirstName: item.FirstName,
            LastName: item.LastName,
            DisplayName: item.DisplayName,
            MustCompleteProfile: item.MustCompleteProfile,
            Role: item.Role,
            IsActive: item.IsActive,
            ExportLimitOverrideMode: item.ExportLimitOverrideMode,
            ExportLimitRowsOverride: item.ExportLimitRowsOverride,
            EffectiveExportLimitMode: item.EffectiveExportLimitMode,
            EffectiveExportLimitRows: item.EffectiveExportLimitRows,
            IsExportLimitOverridden: item.IsExportLimitOverridden,
            IsExportLimitEditable: item.IsExportLimitEditable,
            SuperAdminNote: item.SuperAdminNote);
    }

    private static object ToResponse(AdminUserDetailsDto user, bool includeSuperAdminNote)
    {
        if (includeSuperAdminNote)
        {
            return new SuperAdminUserDetailsResponse(
                Id: user.Id,
                Email: user.Email,
                FirstName: user.FirstName,
                LastName: user.LastName,
                DisplayName: user.DisplayName,
                MustCompleteProfile: user.MustCompleteProfile,
                MustChangePassword: user.MustChangePassword,
                Role: user.Role,
                IsActive: user.IsActive,
                ExportLimitOverrideMode: user.ExportLimitOverrideMode,
                ExportLimitRowsOverride: user.ExportLimitRowsOverride,
                EffectiveExportLimitMode: user.EffectiveExportLimitMode,
                EffectiveExportLimitRows: user.EffectiveExportLimitRows,
                IsExportLimitOverridden: user.IsExportLimitOverridden,
                IsExportLimitEditable: user.IsExportLimitEditable,
                GoogleDriveConnected: user.GoogleDriveConnected,
                GoogleDrive: user.GoogleDrive,
                SuperAdminNote: user.SuperAdminNote);
        }

        return new AdminUserDetailsResponse(
            Id: user.Id,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            DisplayName: user.DisplayName,
            MustCompleteProfile: user.MustCompleteProfile,
            MustChangePassword: user.MustChangePassword,
            Role: user.Role,
            IsActive: user.IsActive,
            ExportLimitOverrideMode: user.ExportLimitOverrideMode,
            ExportLimitRowsOverride: user.ExportLimitRowsOverride,
            EffectiveExportLimitMode: user.EffectiveExportLimitMode,
            EffectiveExportLimitRows: user.EffectiveExportLimitRows,
            IsExportLimitOverridden: user.IsExportLimitOverridden,
            IsExportLimitEditable: user.IsExportLimitEditable,
            GoogleDriveConnected: user.GoogleDriveConnected,
            GoogleDrive: user.GoogleDrive);
    }
}
