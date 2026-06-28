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
            DateTime.UtcNow.AddHours(24));
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
        Assert.Equal(InvitationEmailMessageFactory.Subject, message.Subject);
        var textBody = Assert.IsType<string>(message.TextBody);
        var htmlBody = Assert.IsType<string>(message.HtmlBody);
        Assert.Contains(activationUrl, textBody);
        Assert.Contains(activationUrl.Replace("&", "&amp;", StringComparison.Ordinal), htmlBody);
        Assert.Contains("Activate your Redhead Catalog account", textBody);
        Assert.Contains("Activate your account", htmlBody);
        Assert.Contains("Activate account", htmlBody);
        Assert.Contains("single-use", textBody);
        Assert.Contains("24 hours", textBody);
        Assert.Contains("24 hours", htmlBody);
        Assert.Contains("If you were not expecting this invitation", textBody);
        Assert.Contains("If you were not expecting this invitation", htmlBody);
        Assert.Contains("background-color: #d92d46", htmlBody);
        Assert.Contains("max-width: 600px", htmlBody);
        Assert.True(
            htmlBody.IndexOf("24 hours", StringComparison.Ordinal) <
            htmlBody.IndexOf("If the button does not work", StringComparison.Ordinal));
        Assert.DoesNotContain("password", textBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SuperAdmin", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(message.ReplyTo);
    }
}
