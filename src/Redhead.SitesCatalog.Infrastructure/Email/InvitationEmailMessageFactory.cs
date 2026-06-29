using System.Text.Encodings.Web;
using MimeKit;
using MimeKit.Utils;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Email;

public static class InvitationEmailMessageFactory
{
    public const string Subject = "Activate your Redhead Catalog account";
    private const string LogoFileName = "redhead-digital-logo.png";
    private const string LogoResourceName =
        "Redhead.SitesCatalog.Infrastructure.Email.Assets.redhead-digital-logo.png";
    private static readonly byte[] LogoData = LoadLogoData();

    public static MimeMessage Create(
        InvitationEmailSendRequest request,
        EmailOptions options)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(request.RecipientEmail));
        message.Subject = Subject;

        var encodedUrl = HtmlEncoder.Default.Encode(request.ActivationUrl);
        var logoContentId = MimeUtils.GenerateMessageId();
        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"""
                Activate your Redhead Catalog account

                You've been invited to the Redhead Digital Agency website catalog — your access to our curated database of sites for guest posts, link placements, and outreach campaigns.

                Activate your account to browse available websites, check live metrics and pricing, and start building your placement list.

                Activate your account:
                {request.ActivationUrl}

                This activation link is single-use and expires in {InvitationPolicy.LifetimeHours} hours.

                If you were not expecting this invitation, you can safely ignore this email.

                The Redhead Digital Agency team
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
                    You've been invited to the Redhead Digital Agency website catalog. Your activation link expires in {InvitationPolicy.LifetimeHours} hours.
                  </span>

                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width: 100%; background-color: #f4f4f5;">
                    <tr>
                      <td align="center" style="padding: 32px 16px;">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width: 100%; max-width: 600px; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px;">
                          <tr>
                            <td style="padding: 22px 32px; border-bottom: 1px solid #e5e7eb;">
                              <img src="cid:{logoContentId}" width="190" alt="Redhead Digital Agency" style="display: block; width: 190px; max-width: 100%; height: auto; border: 0;">
                            </td>
                          </tr>
                          <tr>
                            <td style="padding: 32px;">
                              <h1 style="margin: 0 0 16px; font-size: 28px; line-height: 36px; font-weight: 700; color: #262626;">
                                Activate your account
                              </h1>
                              <p style="margin: 0 0 16px; font-size: 16px; line-height: 24px; color: #525252;">
                                You've been invited to the Redhead Digital Agency website catalog — your access to our curated database of sites for guest posts, link placements, and outreach campaigns.
                              </p>
                              <p style="margin: 0 0 24px; font-size: 16px; line-height: 24px; color: #525252;">
                                Activate your account to browse available websites, check live metrics and pricing, and start building your placement list.
                              </p>

                              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin: 0 0 24px;">
                                <tr>
                                  <td bgcolor="#ff7c32" style="border-radius: 8px; background-color: #ff7c32;">
                                    <a href="{encodedUrl}" style="display: inline-block; padding: 13px 24px; font-size: 16px; line-height: 22px; font-weight: 700; color: #262626; text-decoration: none; border-radius: 8px;">
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

                        <p style="margin: 16px 0 0; font-size: 12px; line-height: 18px; color: #737373; text-align: center;">
                          The Redhead Digital Agency team
                        </p>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """
        };

        var logo = bodyBuilder.LinkedResources.Add(
            LogoFileName,
            LogoData,
            new ContentType("image", "png"));
        logo.ContentId = logoContentId;

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private static byte[] LoadLogoData()
    {
        using var resource = typeof(InvitationEmailMessageFactory).Assembly
            .GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException("Embedded invitation email logo was not found.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
