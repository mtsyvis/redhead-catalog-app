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
                Activate your Redhead Catalog account

                You have been invited to Redhead Catalog.

                Activate your account to complete your profile and get started.

                Activate your account:
                {request.ActivationUrl}

                This activation link is single-use and expires in {InvitationPolicy.LifetimeHours} hours.

                If you were not expecting this invitation, you can safely ignore this email.

                Redhead Digital Agency
                """,
            HtmlBody = $"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <title>{Subject}</title>
                </head>
                <body style="margin: 0; padding: 0; background-color: #f4f4f5; color: #262626; font-family: Arial, Helvetica, sans-serif;">
                  <span style="display: none !important; max-height: 0; max-width: 0; overflow: hidden; opacity: 0; color: transparent;">
                    You have been invited to Redhead Catalog. Your activation link expires in {InvitationPolicy.LifetimeHours} hours.
                  </span>

                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width: 100%; background-color: #f4f4f5;">
                    <tr>
                      <td align="center" style="padding: 32px 16px;">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width: 100%; max-width: 600px; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px;">
                          <tr>
                            <td style="padding: 24px 32px; border-bottom: 1px solid #e5e7eb; font-size: 20px; line-height: 28px; font-weight: 700; color: #262626;">
                              Redhead Catalog
                            </td>
                          </tr>
                          <tr>
                            <td style="padding: 32px;">
                              <h1 style="margin: 0 0 16px; font-size: 28px; line-height: 36px; font-weight: 700; color: #262626;">
                                Activate your account
                              </h1>
                              <p style="margin: 0 0 24px; font-size: 16px; line-height: 24px; color: #525252;">
                                You have been invited to Redhead Catalog. Activate your account to complete your profile and get started.
                              </p>

                              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin: 0 0 24px;">
                                <tr>
                                  <td bgcolor="#d92d46" style="border-radius: 8px; background-color: #d92d46;">
                                    <a href="{encodedUrl}" style="display: inline-block; padding: 13px 24px; font-size: 16px; line-height: 22px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: 8px;">
                                      Activate account
                                    </a>
                                  </td>
                                </tr>
                              </table>

                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width: 100%; margin: 0 0 24px;">
                                <tr>
                                  <td style="padding: 14px 16px; background-color: #fff7ed; border-left: 4px solid #f59e0b; font-size: 14px; line-height: 21px; color: #7c2d12;">
                                    This activation link is single-use and expires in <strong>{InvitationPolicy.LifetimeHours} hours</strong>.
                                  </td>
                                </tr>
                              </table>

                              <p style="margin: 0 0 8px; font-size: 14px; line-height: 21px; color: #525252;">
                                If the button does not work, copy and paste this link into your browser:
                              </p>
                              <p style="margin: 0 0 24px; padding: 12px 14px; background-color: #f5f5f5; border-radius: 8px; font-size: 13px; line-height: 20px; word-break: break-all; overflow-wrap: anywhere;">
                                <a href="{encodedUrl}" style="color: #1d4ed8; text-decoration: underline;">{encodedUrl}</a>
                              </p>

                              <p style="margin: 0; font-size: 14px; line-height: 21px; color: #737373;">
                                If you were not expecting this invitation, you can safely ignore this email.
                              </p>
                            </td>
                          </tr>
                        </table>

                        <p style="margin: 16px 0 0; font-size: 12px; line-height: 18px; color: #737373;">
                          Redhead Digital Agency
                        </p>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """
        };

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }
}
