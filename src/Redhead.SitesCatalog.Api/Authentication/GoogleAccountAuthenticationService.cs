using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Authentication;

public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? DisplayName);

public enum GoogleAccountAuthenticationStatus
{
    Success,
    InvalidIdentity,
    EmailConflict,
    Disabled,
    ProvisioningFailed
}

public sealed record GoogleAccountAuthenticationResult(
    GoogleAccountAuthenticationStatus Status,
    ApplicationUser? User = null,
    bool Created = false);

public interface IGoogleAccountAuthenticationService
{
    Task<GoogleAccountAuthenticationResult> AuthenticateAsync(
        GoogleIdentity identity,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleAccountAuthenticationService : IGoogleAccountAuthenticationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<GoogleAccountAuthenticationService> _logger;

    public GoogleAccountAuthenticationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<GoogleAccountAuthenticationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<GoogleAccountAuthenticationResult> AuthenticateAsync(
        GoogleIdentity identity,
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(identity))
        {
            return new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.InvalidIdentity);
        }

        var linkedUser = await _userManager.FindByLoginAsync(
            ExternalLoginProviders.Google,
            identity.Subject);
        if (linkedUser != null)
        {
            if (!linkedUser.IsActive)
            {
                return new GoogleAccountAuthenticationResult(
                    GoogleAccountAuthenticationStatus.Disabled);
            }

            var roles = await _userManager.GetRolesAsync(linkedUser);
            if (roles.Count != 1 || !AppRoles.All.Contains(roles[0], StringComparer.Ordinal))
            {
                _logger.LogWarning(
                    "Google authentication rejected an incomplete account. UserId={UserId}, RoleCount={RoleCount}",
                    linkedUser.Id,
                    roles.Count);
                return new GoogleAccountAuthenticationResult(
                    GoogleAccountAuthenticationStatus.ProvisioningFailed);
            }

            return new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.Success,
                linkedUser);
        }

        if (await _userManager.FindByEmailAsync(identity.Email) != null)
        {
            return new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.EmailConflict);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var displayNameValidation = UserDisplayNameValidator.Validate(identity.DisplayName);
            var user = new ApplicationUser
            {
                UserName = identity.Email,
                Email = identity.Email,
                EmailConfirmed = true,
                DisplayName = displayNameValidation.IsValid
                    ? displayNameValidation.DisplayName
                    : null,
                IsActive = true,
                MustChangePassword = false,
                ActivatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                LogProvisioningFailure("create", createResult);
                return new GoogleAccountAuthenticationResult(
                    IsDuplicate(createResult)
                        ? GoogleAccountAuthenticationStatus.EmailConflict
                        : GoogleAccountAuthenticationStatus.ProvisioningFailed);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Lite);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                LogProvisioningFailure("role", roleResult);
                return new GoogleAccountAuthenticationResult(
                    GoogleAccountAuthenticationStatus.ProvisioningFailed);
            }

            var loginResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(
                    ExternalLoginProviders.Google,
                    identity.Subject,
                    ExternalLoginProviders.Google));
            if (!loginResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                LogProvisioningFailure("external login", loginResult);
                return new GoogleAccountAuthenticationResult(
                    GoogleAccountAuthenticationStatus.ProvisioningFailed);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Google account provisioned. UserId={UserId}, Role={Role}", user.Id, AppRoles.Lite);

            return new GoogleAccountAuthenticationResult(
                GoogleAccountAuthenticationStatus.Success,
                user,
                Created: true);
        });
    }

    private static bool IsValid(GoogleIdentity identity)
        => identity.EmailVerified &&
           !string.IsNullOrWhiteSpace(identity.Subject) &&
           !string.IsNullOrWhiteSpace(identity.Email);

    private static bool IsDuplicate(IdentityResult result)
        => result.Errors.Any(error =>
            string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateEmail), StringComparison.Ordinal) ||
            string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateUserName), StringComparison.Ordinal));

    private void LogProvisioningFailure(string stage, IdentityResult result)
        => _logger.LogWarning(
            "Google account provisioning failed at {Stage}. ErrorCodes={ErrorCodes}",
            stage,
            string.Join(',', result.Errors.Select(error => error.Code)));
}
