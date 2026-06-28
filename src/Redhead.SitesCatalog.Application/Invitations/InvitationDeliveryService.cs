using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Invitations;

public sealed class InvitationDeliveryService : IInvitationDeliveryService
{
    private readonly IInvitationEmailSender _emailSender;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<InvitationDeliveryService> _logger;

    public InvitationDeliveryService(
        IInvitationEmailSender emailSender,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<InvitationDeliveryService> logger)
    {
        _emailSender = emailSender;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<InvitationDeliveryResult> DeliverAsync(
        InvitationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var activationUrl = BuildActivationUrl(request.ActivationPath);
        InvitationEmailSendResult sendResult;

        try
        {
            sendResult = await _emailSender.SendAsync(
                new InvitationEmailSendRequest(
                    request.RecipientEmail,
                    activationUrl,
                    request.InvitationExpiresAtUtc),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            sendResult = new InvitationEmailSendResult(
                InvitationEmailSendStatus.Failed,
                InvitationEmailFailureCategory.Unexpected);
        }

        LogOutcome(request, sendResult);
        return new InvitationDeliveryResult(activationUrl, sendResult.Status);
    }

    private string BuildActivationUrl(string activationPath)
    {
        var baseUrl = _frontendOptions.BaseUrl!.TrimEnd('/') + "/";
        return new Uri(new Uri(baseUrl, UriKind.Absolute), activationPath.TrimStart('/')).AbsoluteUri;
    }

    private void LogOutcome(
        InvitationDeliveryRequest request,
        InvitationEmailSendResult result)
    {
        var maskedRecipient = MaskEmail(request.RecipientEmail);
        if (result.Status == InvitationEmailSendStatus.Failed)
        {
            _logger.LogWarning(
                "Invitation email failed. UserId={UserId}, EventType={EventType}, Recipient={Recipient}, FailureCategory={FailureCategory}",
                request.UserId,
                request.EventType,
                maskedRecipient,
                result.FailureCategory ?? InvitationEmailFailureCategory.Unexpected);
            return;
        }

        _logger.LogInformation(
            "Invitation email outcome. UserId={UserId}, EventType={EventType}, Recipient={Recipient}, Status={Status}",
            request.UserId,
            request.EventType,
            maskedRecipient,
            result.Status);
    }

    private static string MaskEmail(string email)
    {
        var separatorIndex = email.IndexOf('@');
        if (separatorIndex <= 0 || separatorIndex == email.Length - 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[separatorIndex..]}";
    }
}
