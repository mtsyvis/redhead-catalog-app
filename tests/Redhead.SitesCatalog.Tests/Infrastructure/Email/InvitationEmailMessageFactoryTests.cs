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
            DateTime.UtcNow.AddHours(72));
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
        Assert.Contains(activationUrl, message.TextBody);
        Assert.Contains(activationUrl.Replace("&", "&amp;", StringComparison.Ordinal), message.HtmlBody);
        Assert.Contains("Activate account", message.HtmlBody);
        Assert.Contains("single-use", message.TextBody);
        Assert.Contains("72 hours", message.HtmlBody);
        Assert.DoesNotContain("password", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(message.ReplyTo);
    }
}
