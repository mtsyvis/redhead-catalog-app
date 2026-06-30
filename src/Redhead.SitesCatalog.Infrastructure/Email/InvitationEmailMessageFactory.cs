using System.Text.Encodings.Web;
using MimeKit;
using MimeKit.Utils;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Email;

public static class InvitationEmailMessageFactory
{
    public const string Subject = "Activate your Redhead Catalog account";
    private const string LightLogoFileName = "redhead-digital-logo-light.png";
    private const string LightLogoResourceName =
        "Redhead.SitesCatalog.Infrastructure.Email.Assets.redhead-digital-logo-light.png";
    private const string DarkLogoFileName = "redhead-digital-logo-dark.png";
    private const string DarkLogoResourceName =
        "Redhead.SitesCatalog.Infrastructure.Email.Assets.redhead-digital-logo-dark.png";
    private static readonly byte[] LightLogoData = LoadLogoData(LightLogoResourceName);
    private static readonly byte[] DarkLogoData = LoadLogoData(DarkLogoResourceName);

    public static MimeMessage Create(
        InvitationEmailSendRequest request,
        EmailOptions options)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(request.RecipientEmail));
        message.Subject = Subject;

        var encodedUrl = HtmlEncoder.Default.Encode(request.ActivationUrl);
        var lightLogoContentId = MimeUtils.GenerateMessageId();
        var darkLogoContentId = MimeUtils.GenerateMessageId();
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
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <meta name="color-scheme" content="light dark">
                  <meta name="supported-color-schemes" content="light dark">
                  <title>{{Subject}}</title>
                  <style>
                    :root { color-scheme: light dark; supported-color-schemes: light dark; }
                    .dark-logo { display: none !important; }

                    @media (prefers-color-scheme: dark) {
                      .email-body, .email-bg { background-color: #1f1f1f !important; }
                      .email-card { background-color: #2f2f2f !important; border-color: #454545 !important; }
                      .email-header { border-color: #454545 !important; }
                      .email-title { color: #f5f5f5 !important; }
                      .email-copy, .email-link-label { color: #d4d4d4 !important; }
                      .email-expiry { background-color: #3b2d21 !important; color: #fed7aa !important; }
                      .email-link-box { background-color: #262626 !important; }
                      .email-link { color: #93c5fd !important; }
                      .email-muted, .email-footer { color: #b8b8b8 !important; }
                      .light-logo { display: none !important; }
                      .dark-logo { display: block !important; }
                    }

                    [data-ogsc] .light-logo { display: none !important; }
                    [data-ogsc] .dark-logo { display: block !important; }
                  </style>
                </head>
                <body class="email-body" style="margin: 0; padding: 0; background-color: #f4f4f5; color: #262626; font-family: Arial, Helvetica, sans-serif;">
                  <span style="display: none !important; max-height: 0; max-width: 0; overflow: hidden; opacity: 0; color: transparent;">
                    You've been invited to the Redhead Digital Agency website catalog. Your activation link expires in {{InvitationPolicy.LifetimeHours}} hours.
                  </span>

                  <table class="email-bg" role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width: 100%; background-color: #f4f4f5;">
                    <tr>
                      <td align="center" style="padding: 32px 16px;">
                        <table class="email-card" role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width: 100%; max-width: 600px; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px;">
                          <tr>
                            <td class="email-header" style="padding: 22px 32px; border-bottom: 1px solid #e5e7eb;">
                              <table role="presentation" width="190" cellpadding="0" cellspacing="0" border="0" style="width: 190px; max-width: 190px;">
                                <tr>
                                  <td width="190" style="width: 190px; max-width: 190px; font-size: 0; line-height: 0;">
                                    <img class="light-logo" src="cid:{{lightLogoContentId}}" width="190" height="53" alt="Redhead Digital Agency" style="display: block; width: 190px !important; max-width: 190px !important; height: 53px !important; border: 0;">
                                    <img class="dark-logo" src="cid:{{darkLogoContentId}}" width="190" height="53" alt="Redhead Digital Agency" style="display: none; mso-hide: all; width: 190px !important; max-width: 190px !important; height: 53px !important; border: 0;">
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding: 32px;">
                              <h1 class="email-title" style="margin: 0 0 16px; font-size: 28px; line-height: 36px; font-weight: 700; color: #262626;">
                                Activate your account
                              </h1>
                              <p class="email-copy" style="margin: 0 0 16px; font-size: 16px; line-height: 24px; color: #525252;">
                                You've been invited to the Redhead Digital Agency website catalog — your access to our curated database of sites for guest posts, link placements, and outreach campaigns.
                              </p>
                              <p class="email-copy" style="margin: 0 0 24px; font-size: 16px; line-height: 24px; color: #525252;">
                                Activate your account to browse available websites, check live metrics and pricing, and start building your placement list.
                              </p>

                              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin: 0 0 24px;">
                                <tr>
                                  <td bgcolor="#ff7c32" style="border-radius: 8px; background-color: #ff7c32;">
                                    <a href="{{encodedUrl}}" style="display: inline-block; padding: 13px 24px; font-size: 16px; line-height: 22px; font-weight: 700; color: #262626; text-decoration: none; border-radius: 8px;">
                                      Activate account
                                    </a>
                                  </td>
                                </tr>
                              </table>

                              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width: 100%; margin: 0 0 24px;">
                                <tr>
                                  <td class="email-expiry" style="padding: 14px 16px; background-color: #fff7ed; border-left: 4px solid #f59e0b; font-size: 14px; line-height: 21px; color: #7c2d12;">
                                    This activation link is single-use and expires in <strong>{{InvitationPolicy.LifetimeHours}} hours</strong>.
                                  </td>
                                </tr>
                              </table>

                              <p class="email-link-label" style="margin: 0 0 8px; font-size: 14px; line-height: 21px; color: #525252;">
                                If the button does not work, copy and paste this link into your browser:
                              </p>
                              <p class="email-link-box" style="margin: 0 0 24px; padding: 12px 14px; background-color: #f5f5f5; border-radius: 8px; font-size: 13px; line-height: 20px; word-break: break-all; overflow-wrap: anywhere;">
                                <a class="email-link" href="{{encodedUrl}}" style="color: #1d4ed8; text-decoration: underline;">{{encodedUrl}}</a>
                              </p>

                              <p class="email-muted" style="margin: 0; font-size: 14px; line-height: 21px; color: #737373;">
                                If you were not expecting this invitation, you can safely ignore this email.
                              </p>
                            </td>
                          </tr>
                        </table>

                        <p class="email-footer" style="margin: 16px 0 0; font-size: 12px; line-height: 18px; color: #737373; text-align: center;">
                          The Redhead Digital Agency team
                        </p>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """
        };

        var lightLogo = bodyBuilder.LinkedResources.Add(
            LightLogoFileName,
            LightLogoData,
            new ContentType("image", "png"));
        lightLogo.ContentId = lightLogoContentId;

        var darkLogo = bodyBuilder.LinkedResources.Add(
            DarkLogoFileName,
            DarkLogoData,
            new ContentType("image", "png"));
        darkLogo.ContentId = darkLogoContentId;

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private static byte[] LoadLogoData(string resourceName)
    {
        using var resource = typeof(InvitationEmailMessageFactory).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded invitation email logo was not found.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
