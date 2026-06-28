using System.Text.Encodings.Web;
using MimeKit;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Email;

public static class InvitationEmailMessageFactory
{
    public const string Subject = "Activate your Redhead Catalog account";

    public static MimeMessage Create(
        InvitationEmailSendRequest request,
        EmailOptions options)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(request.RecipientEmail));
        message.Subject = Subject;

        var encodedUrl = HtmlEncoder.Default.Encode(request.ActivationUrl);
        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"""
                You have been invited to Redhead Catalog.

                Activate your account:
                {request.ActivationUrl}

                This activation link is single-use and expires in 72 hours.
                """,
            HtmlBody = $"""
                <!doctype html>
                <html lang="en">
                <body style="font-family: Arial, sans-serif; color: #262626; line-height: 1.5;">
                  <p>You have been invited to Redhead Catalog.</p>
                  <p>
                    <a href="{encodedUrl}" style="display: inline-block; padding: 12px 20px; border-radius: 6px; background: #ff455b; color: #ffffff; text-decoration: none;">
                      Activate account
                    </a>
                  </p>
                  <p>If the button does not work, open this link:</p>
                  <p style="word-break: break-all;"><a href="{encodedUrl}">{encodedUrl}</a></p>
                  <p>This activation link is single-use and expires in 72 hours.</p>
                </body>
                </html>
                """
        };

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }
}
