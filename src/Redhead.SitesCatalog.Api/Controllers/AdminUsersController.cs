using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    [HttpPost]
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
    public async Task<ActionResult<IReadOnlyList<UserListItem>>> ListUsers(CancellationToken cancellationToken)
    {
        var users = _userManager.Users.ToList();
        var roleSettingsList = await _context.RoleSettings.ToListAsync(cancellationToken);
        var roleSettingsMap = roleSettingsList.ToDictionary(rs => rs.RoleName);

        var list = new List<UserListItem>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? string.Empty;
            var isSuperAdmin = string.Equals(role, AppRoles.SuperAdmin, StringComparison.Ordinal);

            var roleSettings = roleSettingsMap.TryGetValue(role, out var rs)
                ? rs
                : new RoleSettings { RoleName = role, ExportLimitMode = ExportLimitMode.Disabled };

            var effectivePolicy = EffectiveExportPolicyResolver.Resolve(role, roleSettings, user);

            list.Add(new UserListItem(
                Id: user.Id,
                Email: user.Email ?? string.Empty,
                Role: role,
                IsActive: user.IsActive,
                ExportLimitOverrideMode: isSuperAdmin ? null : user.ExportLimitOverrideMode,
                ExportLimitRowsOverride: isSuperAdmin ? null : user.ExportLimitRowsOverride,
                EffectiveExportLimitMode: effectivePolicy.Mode,
                EffectiveExportLimitRows: effectivePolicy.Rows,
                IsExportLimitOverridden: effectivePolicy.IsOverridden,
                IsExportLimitEditable: !isSuperAdmin));
        }

        var orderedList = list
            .OrderBy(item => item.IsActive ? 0 : 1)
            .ThenBy(item => GetRoleOrder(item.Role))
            .ThenBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(orderedList);
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

    private static int GetRoleOrder(string role) => role switch
    {
        AppRoles.SuperAdmin => 0,
        AppRoles.Admin => 1,
        AppRoles.Internal => 2,
        AppRoles.Client => 3,
        _ => int.MaxValue,
    };
}
