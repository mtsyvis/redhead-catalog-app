using Redhead.SitesCatalog.Api.Models;

namespace Redhead.SitesCatalog.Api.AccountSetup;

public sealed class AccountSetupCompletionResult
{
    private AccountSetupCompletionResult(
        AccountSetupCompletionStatus status,
        CompleteAccountSetupResponse? response = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        IReadOnlyList<string>? errors = null)
    {
        Status = status;
        Response = response;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
        Errors = errors ?? [];
    }

    public AccountSetupCompletionStatus Status { get; }
    public CompleteAccountSetupResponse? Response { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }
    public IReadOnlyList<string> Errors { get; }

    public static AccountSetupCompletionResult Success(CompleteAccountSetupResponse response)
        => new(AccountSetupCompletionStatus.Success, response);

    public static AccountSetupCompletionResult ValidationFailed(
        IReadOnlyDictionary<string, string[]> errors)
        => new(AccountSetupCompletionStatus.ValidationFailed, validationErrors: errors);

    public static AccountSetupCompletionResult PasswordChangeFailed(IEnumerable<string> errors)
        => new(AccountSetupCompletionStatus.PasswordChangeFailed, errors: errors.ToList());

    public static AccountSetupCompletionResult UserUpdateFailed(IEnumerable<string> errors)
        => new(AccountSetupCompletionStatus.UserUpdateFailed, errors: errors.ToList());
}

public enum AccountSetupCompletionStatus
{
    Success,
    ValidationFailed,
    PasswordChangeFailed,
    UserUpdateFailed
}
