using Redhead.SitesCatalog.Domain.Invitations;

namespace Redhead.SitesCatalog.Application.Invitations;

public interface IInvitationDeliveryService
{
    Task<InvitationDeliveryResult> DeliverAsync(
        InvitationDeliveryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record InvitationDeliveryRequest(
    string UserId,
    string RecipientEmail,
    string ActivationPath,
    DateTime InvitationExpiresAtUtc,
    InvitationEventType EventType,
    AccountAccessEmailKind EmailKind = AccountAccessEmailKind.Activation);

public sealed record InvitationDeliveryResult(
    string ActivationUrl,
    InvitationEmailSendStatus EmailDeliveryStatus);

public enum InvitationEventType
{
    Create,
    Reissue,
    ReactivateNeverActivated,
    ReactivateActivated,
    ReissueReactivation
}
