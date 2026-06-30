namespace Redhead.SitesCatalog.Domain.Invitations;

public interface IInvitationEmailSender
{
    Task<InvitationEmailSendResult> SendAsync(
        InvitationEmailSendRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record InvitationEmailSendRequest(
    string RecipientEmail,
    string ActivationUrl,
    DateTime InvitationExpiresAtUtc,
    AccountAccessEmailKind EmailKind = AccountAccessEmailKind.Activation);

public enum AccountAccessEmailKind
{
    Activation,
    Reactivation
}

public sealed record InvitationEmailSendResult(
    InvitationEmailSendStatus Status,
    InvitationEmailFailureCategory? FailureCategory = null);

public enum InvitationEmailSendStatus
{
    Sent,
    NotAttemptedBecauseEmailDisabled,
    Failed
}

public enum InvitationEmailFailureCategory
{
    Timeout,
    Connection,
    Tls,
    Rejected,
    Protocol,
    Unexpected
}
