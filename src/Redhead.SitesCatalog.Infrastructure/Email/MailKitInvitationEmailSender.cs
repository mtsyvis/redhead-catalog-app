using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Email;

public sealed class MailKitInvitationEmailSender : IInvitationEmailSender
{
    private readonly ISmtpClient _smtpClient;
    private readonly EmailOptions _options;

    public MailKitInvitationEmailSender(
        ISmtpClient smtpClient,
        IOptions<EmailOptions> options)
    {
        _smtpClient = smtpClient;
        _options = options.Value;
    }

    public async Task<InvitationEmailSendResult> SendAsync(
        InvitationEmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new InvitationEmailSendResult(
                InvitationEmailSendStatus.NotAttemptedBecauseEmailDisabled);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.SendTimeoutSeconds));

        try
        {
            var message = InvitationEmailMessageFactory.Create(request, _options);
            await _smtpClient.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                SecureSocketOptions.StartTls,
                timeoutSource.Token);
            await _smtpClient.SendAsync(message, timeoutSource.Token);

            return new InvitationEmailSendResult(InvitationEmailSendStatus.Sent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(InvitationEmailFailureCategory.Timeout);
        }
        catch (SslHandshakeException)
        {
            return Failed(InvitationEmailFailureCategory.Tls);
        }
        catch (SmtpCommandException)
        {
            return Failed(InvitationEmailFailureCategory.Rejected);
        }
        catch (SmtpProtocolException)
        {
            return Failed(InvitationEmailFailureCategory.Protocol);
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            return Failed(InvitationEmailFailureCategory.Connection);
        }
        catch
        {
            return Failed(InvitationEmailFailureCategory.Unexpected);
        }
        finally
        {
            if (_smtpClient.IsConnected)
            {
                try
                {
                    await _smtpClient.DisconnectAsync(true, timeoutSource.Token);
                }
                catch
                {
                    // Sending has already succeeded or failed; disconnect errors do not change the outcome.
                }
            }
        }
    }

    private static InvitationEmailSendResult Failed(InvitationEmailFailureCategory category)
        => new(InvitationEmailSendStatus.Failed, category);
}
