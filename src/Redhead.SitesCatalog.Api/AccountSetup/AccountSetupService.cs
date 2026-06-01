using Microsoft.AspNetCore.Identity;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.AccountSetup;

public sealed class AccountSetupService : IAccountSetupService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountSetupService> _logger;

    public AccountSetupService(
        UserManager<ApplicationUser> userManager,
        ILogger<AccountSetupService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<AccountSetupCompletionResult> CompleteAsync(
        ApplicationUser user,
        CompleteAccountSetupRequest request)
    {
        var requiredParts = AccountSetupParts.From(user);
        var validationErrors = ValidateRequest(request, requiredParts);
        if (validationErrors.Count > 0)
        {
            return AccountSetupCompletionResult.ValidationFailed(validationErrors);
        }

        if (!requiredParts.ShouldChangePassword && !requiredParts.ShouldUpdateProfile)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            return AccountSetupCompletionResult.Success(ToResponse(user, currentRoles));
        }

        if (requiredParts.ShouldChangePassword)
        {
            var passwordResult = await ChangePasswordAsync(user, request);
            if (passwordResult != null)
            {
                return passwordResult;
            }
        }

        if (requiredParts.ShouldUpdateProfile)
        {
            ApplyProfileNames(user, request);
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return AccountSetupCompletionResult.UserUpdateFailed(
                updateResult.Errors.Select(error => error.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        return AccountSetupCompletionResult.Success(ToResponse(user, roles));
    }

    private async Task<AccountSetupCompletionResult?> ChangePasswordAsync(
        ApplicationUser user,
        CompleteAccountSetupRequest request)
    {
        var passwordResult = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword!,
            request.NewPassword!);

        if (!passwordResult.Succeeded)
        {
            _logger.LogWarning(
                "CompleteAccountSetup password change failed for {Email}: {Errors}",
                user.Email,
                string.Join(", ", passwordResult.Errors.Select(error => error.Description)));

            return AccountSetupCompletionResult.PasswordChangeFailed(
                passwordResult.Errors.Select(error => error.Description));
        }

        user.MustChangePassword = false;
        return null;
    }

    private static Dictionary<string, string[]> ValidateRequest(
        CompleteAccountSetupRequest request,
        AccountSetupParts parts)
    {
        var errors = new Dictionary<string, string[]>();

        if (parts.ShouldChangePassword)
        {
            AddPasswordErrors(request, errors);
        }

        if (parts.ShouldUpdateProfile)
        {
            var profileValidation = UserProfileNameValidator.Validate(request.FirstName, request.LastName);
            foreach (var error in profileValidation.Errors)
            {
                errors[error.Key] = error.Value;
            }
        }

        return errors;
    }

    private static void AddPasswordErrors(
        CompleteAccountSetupRequest request,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            errors["currentPassword"] = ["Current password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            errors["newPassword"] = ["New password is required."];
        }
    }

    private static void ApplyProfileNames(
        ApplicationUser user,
        CompleteAccountSetupRequest request)
    {
        var profileValidation = UserProfileNameValidator.Validate(request.FirstName, request.LastName);
        user.FirstName = profileValidation.FirstName;
        user.LastName = profileValidation.LastName;
    }

    private static CompleteAccountSetupResponse ToResponse(
        ApplicationUser user,
        IList<string> roles)
    {
        return new CompleteAccountSetupResponse(
            user.Email!,
            user.MustChangePassword,
            !user.HasCompleteProfile,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            roles);
    }

    private sealed record AccountSetupParts(bool ShouldChangePassword, bool ShouldUpdateProfile)
    {
        public static AccountSetupParts From(ApplicationUser user)
        {
            return new AccountSetupParts(
                user.MustChangePassword,
                !user.HasCompleteProfile);
        }
    }
}
