using MimeKit;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Email;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Email;

public sealed class InvitationEmailMessageFactoryTests
{
    [Fact]
    public void Create_BuildsSafeHtmlAndPlainTextInvitation()
    {
        // Arrange
        const string activationUrl = "https://catalog.rhda.us/activate-account?token=abc123";
        var request = new InvitationEmailSendRequest(
            "invited@example.com",
            activationUrl,
            DateTime.UtcNow.AddHours(48));
        var options = new EmailOptions
        {
            FromName = "Redhead Catalog",
            FromAddress = "noreply@redheaddigital.agency"
        };

        // Act
        var message = InvitationEmailMessageFactory.Create(request, options);

        // Assert
        var from = Assert.IsType<MailboxAddress>(Assert.Single(message.From));
        Assert.Equal("Redhead Catalog", from.Name);
        Assert.Equal("noreply@redheaddigital.agency", from.Address);
        Assert.Equal("invited@example.com", Assert.IsType<MailboxAddress>(Assert.Single(message.To)).Address);
        Assert.Equal(InvitationEmailMessageFactory.ActivationSubject, message.Subject);
        var textBody = Assert.IsType<string>(message.TextBody);
        var htmlBody = Assert.IsType<string>(message.HtmlBody);
        Assert.Contains(activationUrl, textBody);
        Assert.Contains(activationUrl.Replace("&", "&amp;", StringComparison.Ordinal), htmlBody);
        Assert.Contains("Activate your account", textBody);
        Assert.Contains("Activate your account", htmlBody);
        Assert.Contains("Activate account", htmlBody);
        Assert.Contains(
            "You've been invited to the Redhead Digital Agency website catalog",
            textBody);
        Assert.Contains(
            "your access to our curated database of sites for guest posts, link placements, and outreach campaigns",
            htmlBody);
        Assert.Contains(
            "browse available websites, check live metrics and pricing, and start building your placement list",
            textBody);
        Assert.Contains("The Redhead Digital Agency team", textBody);
        Assert.Contains("The Redhead Digital Agency team", htmlBody);
        Assert.Contains(
            "margin: 16px 0 0; font-size: 12px; line-height: 18px; color: #737373; text-align: center;",
            htmlBody);
        Assert.DoesNotContain("font-weight: 700; color: #41302b;", htmlBody);
        Assert.Equal(1, htmlBody.Split("The Redhead Digital Agency team").Length - 1);
        Assert.Contains("single-use", textBody);
        Assert.Contains("48 hours", textBody);
        Assert.Contains("48 hours", htmlBody);
        Assert.Contains("If you were not expecting this invitation", textBody);
        Assert.Contains("If you were not expecting this invitation", htmlBody);
        Assert.Contains("background-color: #ff7c32", htmlBody);
        Assert.Contains("max-width: 600px", htmlBody);
        Assert.Contains("prefers-color-scheme: dark", htmlBody);
        Assert.Contains("[data-ogsc] .dark-logo", htmlBody);
        Assert.Contains("width=\"190\" height=\"53\"", htmlBody);
        Assert.Contains("max-width: 190px !important", htmlBody);
        var logos = message.BodyParts.OfType<MimePart>()
            .Where(part => part.ContentType.MimeType == "image/png")
            .OrderBy(part => part.FileName)
            .ToArray();
        Assert.Equal(2, logos.Length);
        Assert.Equal(
            ["redhead-digital-logo-dark.png", "redhead-digital-logo-light.png"],
            logos.Select(logo => logo.FileName));
        Assert.All(logos, logo =>
        {
            Assert.NotNull(logo.ContentId);
            Assert.False(logo.IsAttachment);
            Assert.Contains($"src=\"cid:{logo.ContentId}\"", htmlBody);
            using var logoContent = new MemoryStream();
            Assert.IsType<MimeContent>(logo.Content).DecodeTo(logoContent);
            var logoBytes = logoContent.ToArray();
            Assert.NotEmpty(logoBytes);
            Assert.Equal(380, ReadPngDimension(logoBytes, offset: 16));
            Assert.Equal(106, ReadPngDimension(logoBytes, offset: 20));
        });
        Assert.DoesNotContain("<img src=\"http", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            htmlBody.IndexOf("48 hours", StringComparison.Ordinal) <
            htmlBody.IndexOf("If the button does not work", StringComparison.Ordinal));
        Assert.DoesNotContain("password", textBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SuperAdmin", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(message.ReplyTo);
        Assert.Empty(message.Attachments);
    }

    [Fact]
    public void Create_ForReactivation_BuildsStandaloneReactivationMessageWithoutSecrets()
    {
        // Arrange
        const string reactivationUrl = "https://catalog.rhda.us/reactivate-account?token=abc123";
        var request = new InvitationEmailSendRequest(
            "returning@example.com",
            reactivationUrl,
            DateTime.UtcNow.AddHours(48),
            AccountAccessEmailKind.Reactivation);
        var options = new EmailOptions
        {
            FromName = "Redhead Catalog",
            FromAddress = "noreply@redheaddigital.agency"
        };

        // Act
        var message = InvitationEmailMessageFactory.Create(request, options);

        // Assert
        Assert.Equal(InvitationEmailMessageFactory.ReactivationSubject, message.Subject);
        var textBody = Assert.IsType<string>(message.TextBody);
        var htmlBody = Assert.IsType<string>(message.HtmlBody);
        Assert.Contains("Reactivate your account", textBody);
        Assert.Contains("Reactivate account", htmlBody);
        Assert.Contains("48 hours", textBody);
        Assert.Contains("48 hours", htmlBody);
        Assert.Contains("Choose a new password", textBody);
        Assert.Contains("remains disabled", htmlBody);
        Assert.Contains(reactivationUrl, textBody);
        Assert.DoesNotContain("invited", textBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporary password", textBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SuperAdmin", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Client", htmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadPngDimension(byte[] pngBytes, int offset)
        => (pngBytes[offset] << 24)
           | (pngBytes[offset + 1] << 16)
           | (pngBytes[offset + 2] << 8)
           | pngBytes[offset + 3];
}
