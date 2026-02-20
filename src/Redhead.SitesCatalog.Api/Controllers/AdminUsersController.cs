using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(UserManager<ApplicationUser> userManager, ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
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
    public async Task<ActionResult<IReadOnlyList<UserListItem>>> ListUsers()
    {
        var users = _userManager.Users.ToList();
        var list = new List<UserListItem>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? string.Empty;
            list.Add(new UserListItem(u.Id, u.Email ?? string.Empty, role, u.IsActive));
        }

        return Ok(list);
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
}
