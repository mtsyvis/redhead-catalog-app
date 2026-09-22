using Redhead.SitesCatalog.Domain.ClientCatalog;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Email;

public sealed class ClientCatalogAlertEmailSender(ISmtpClient smtp, IOptions<EmailOptions> emailOptions,
    IOptions<ClientCatalogOptions> catalogOptions, IOptions<FrontendOptions> frontendOptions,
    ILogger<ClientCatalogAlertEmailSender> logger) : IClientCatalogAlertEmailSender
{
    public async Task<bool> SendAsync(ClientCatalogAlert alert, string userEmail, CancellationToken cancellationToken)
    {
        var usersUrl = new Uri(new Uri(frontendOptions.Value.BaseUrl!), "admin/users");
        var body = $"Account: {userEmail}\nAlert: {alert.Id}\n" +
                   $"Detected (UTC): {alert.DetectedAtUtc:yyyy-MM-dd HH:mm}\n" +
                   $"Peak hourly unique sites: {alert.UniqueSites:N0}\n" +
                   $"Notification threshold: {alert.Threshold:N0}\n\n" +
                   "This is a request to review activity, not proof of scraping. " +
                   "The hourly alert does not disable the account.\n\n" +
                   $"Review the Suspicious activity badge in Users: {usersUrl}";
        return await SendMessageAsync("Catalog activity requires review", body, "Catalog alert", alert.Id, cancellationToken);
    }

    public async Task<bool> SendAutoBanAsync(
        ClientCatalogAutoBan autoBan,
        string userEmail,
        CancellationToken cancellationToken)
    {
        var userUrl = new Uri(new Uri(frontendOptions.Value.BaseUrl!), $"admin/users/{Uri.EscapeDataString(autoBan.UserId)}");
        var body = $"Account: {userEmail}\nAutomatic ban: {autoBan.Id}\n" +
                   $"Detected (UTC): {autoBan.DetectedAtUtc:yyyy-MM-dd HH:mm}\n" +
                   $"Unique sites in the rolling 24-hour window: {autoBan.UniqueSites:N0}\n" +
                   $"Automatic-ban threshold: {autoBan.Threshold:N0}\n\n" +
                   "The account was disabled automatically because suspicious catalog activity reached the configured threshold.\n\n" +
                   $"Review the account and decide whether to reactivate it: {userUrl}";
        return await SendMessageAsync("Catalog account disabled automatically", body, "Catalog auto-ban", autoBan.Id, cancellationToken);
    }

    private async Task<bool> SendMessageAsync(
        string subject,
        string body,
        string logLabel,
        long incidentId,
        CancellationToken cancellationToken)
    {
        var email = emailOptions.Value;
        var recipients = catalogOptions.Value.Recipients;
        if (!email.Enabled || recipients.Length == 0)
        {
            return false;
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(email.SendTimeoutSeconds));
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
            foreach (var recipient in recipients)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };
            await smtp.ConnectAsync(email.SmtpHost, email.SmtpPort, SecureSocketOptions.StartTls, timeout.Token);
            await smtp.SendAsync(message, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("{LogLabel} {IncidentId} email failed ({FailureType}); it will be retried",
                logLabel, incidentId, exception.GetType().Name);
            return false;
        }
        finally
        {
            if (smtp.IsConnected)
            {
                try { await smtp.DisconnectAsync(true, timeout.Token); }
                catch { /* Preserve the send outcome; the pending alert remains in the database. */ }
            }
        }
    }
}
