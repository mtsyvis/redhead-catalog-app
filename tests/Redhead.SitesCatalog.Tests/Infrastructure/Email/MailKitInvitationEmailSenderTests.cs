using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Moq;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Email;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Email;

public sealed class MailKitInvitationEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WhenDisabled_DoesNotConnect()
    {
        // Arrange
        var smtpClient = new Mock<ISmtpClient>(MockBehavior.Strict);
        var sut = new MailKitInvitationEmailSender(
            smtpClient.Object,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { Enabled = false }));

        // Act
        var result = await sut.SendAsync(CreateRequest());

        // Assert
        Assert.Equal(InvitationEmailSendStatus.NotAttemptedBecauseEmailDisabled, result.Status);
        smtpClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_WhenEnabled_UsesRequiredStartTlsWithoutAuthentication()
    {
        // Arrange
        var smtpClient = new Mock<ISmtpClient>();
        smtpClient
            .Setup(client => client.ConnectAsync(
                "smtp-relay.gmail.com",
                587,
                SecureSocketOptions.StartTls,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        smtpClient
            .Setup(client => client.SendAsync(
                It.IsAny<MimeMessage>(),
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync("accepted");
        var sut = new MailKitInvitationEmailSender(
            smtpClient.Object,
            Microsoft.Extensions.Options.Options.Create(CreateEnabledOptions()));

        // Act
        var result = await sut.SendAsync(CreateRequest());

        // Assert
        Assert.Equal(InvitationEmailSendStatus.Sent, result.Status);
        smtpClient.Verify(client => client.ConnectAsync(
            "smtp-relay.gmail.com",
            587,
            SecureSocketOptions.StartTls,
            It.IsAny<CancellationToken>()), Times.Once);
        smtpClient.Verify(client => client.SendAsync(
            It.IsAny<MimeMessage>(),
            It.IsAny<CancellationToken>(),
            null), Times.Once);
        smtpClient.Verify(client => client.AuthenticateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static InvitationEmailSendRequest CreateRequest()
        => new(
            "invited@example.com",
            "https://catalog.rhda.us/activate-account?token=abc123",
            DateTime.UtcNow.AddHours(24));

    private static EmailOptions CreateEnabledOptions()
        => new()
        {
            Enabled = true,
            SmtpHost = "smtp-relay.gmail.com",
            SmtpPort = 587,
            FromName = "Redhead Catalog",
            FromAddress = "noreply@redheaddigital.agency",
            SendTimeoutSeconds = 10
        };
}
