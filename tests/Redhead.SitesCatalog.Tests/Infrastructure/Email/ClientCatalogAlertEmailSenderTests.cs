using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using TestOptions = Microsoft.Extensions.Options.Options;
using MimeKit;
using Moq;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Email;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Email;

public sealed class ClientCatalogAlertEmailSenderTests
{
    [Theory]
    [InlineData(false, "admin@example.com")]
    [InlineData(true, "")]
    public async Task SendAsync_WhenNotConfigured_DoesNotContactSmtp(bool enabled, string recipients)
    {
        // Arrange
        var smtp = new Mock<ISmtpClient>(MockBehavior.Strict);
        var sut = CreateSender(smtp.Object, enabled, recipients);

        // Act
        var sent = await sut.SendAsync(CreateAlert(), "client@example.com", CancellationToken.None);

        // Assert
        Assert.False(sent);
        smtp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_SendsOneReviewMessageToConfiguredRecipientsUsingStartTls()
    {
        // Arrange
        var smtp = new Mock<ISmtpClient>();
        MimeMessage? message = null;
        smtp.Setup(client => client.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        smtp.Setup(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null))
            .Callback<MimeMessage, CancellationToken, MailKit.ITransferProgress?>((value, _, _) => message = value)
            .ReturnsAsync("accepted");
        var sut = CreateSender(smtp.Object, true, "admin@example.com; owner@example.com, admin@example.com");

        // Act
        var sent = await sut.SendAsync(CreateAlert(), "client@example.com", CancellationToken.None);

        // Assert
        Assert.True(sent);
        Assert.NotNull(message);
        Assert.Equal(new[] { "admin@example.com", "owner@example.com" }, message.To.Mailboxes.Select(mailbox => mailbox.Address));
        Assert.Contains("client@example.com", message.TextBody);
        Assert.Contains("Peak hourly unique sites:", message.TextBody);
        Assert.Contains("https://catalog.example.com/admin/users", message.TextBody);
        Assert.Contains("does not disable the account", message.TextBody);
        smtp.Verify(client => client.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, It.IsAny<CancellationToken>()), Times.Once);
        smtp.Verify(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpFails_ReportsUnsentForRetry()
    {
        // Arrange
        var smtp = new Mock<ISmtpClient>();
        smtp.Setup(client => client.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("SMTP unavailable"));
        var sut = CreateSender(smtp.Object, true, "admin@example.com");

        // Act
        var sent = await sut.SendAsync(CreateAlert(), "client@example.com", CancellationToken.None);

        // Assert
        Assert.False(sent);
        smtp.Verify(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null), Times.Never);
    }

    [Fact]
    public async Task SendAutoBanAsync_SendsDisabledAccountDetailsAndUserLink()
    {
        // Arrange
        var smtp = new Mock<ISmtpClient>();
        MimeMessage? message = null;
        smtp.Setup(client => client.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        smtp.Setup(client => client.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null))
            .Callback<MimeMessage, CancellationToken, MailKit.ITransferProgress?>((value, _, _) => message = value)
            .ReturnsAsync("accepted");
        var sut = CreateSender(smtp.Object, true, "admin@example.com");
        var autoBan = new ClientCatalogAutoBan
        {
            Id = 7,
            UserId = "client-id",
            DetectedAtUtc = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc),
            UniqueSites = 20_000,
            Threshold = 20_000
        };

        // Act
        var sent = await sut.SendAutoBanAsync(autoBan, "client@example.com", CancellationToken.None);

        // Assert
        Assert.True(sent);
        Assert.NotNull(message);
        Assert.Equal("Catalog account disabled automatically", message.Subject);
        Assert.Contains("Unique sites in the rolling 24-hour window:", message.TextBody);
        Assert.Contains("Automatic-ban threshold:", message.TextBody);
        Assert.Contains("disabled automatically", message.TextBody);
        Assert.Contains("https://catalog.example.com/admin/users/client-id", message.TextBody);
    }

    private static ClientCatalogAlertEmailSender CreateSender(ISmtpClient smtp, bool enabled, string recipients)
        => new(smtp,
            TestOptions.Create(new EmailOptions
            {
                Enabled = enabled, SmtpHost = "smtp.example.com", SmtpPort = 587,
                FromName = "Catalog", FromAddress = "catalog@example.com", SendTimeoutSeconds = 10
            }),
            TestOptions.Create(new ClientCatalogOptions { AlertEmails = recipients }),
            TestOptions.Create(new FrontendOptions { BaseUrl = "https://catalog.example.com" }),
            NullLogger<ClientCatalogAlertEmailSender>.Instance);

    private static ClientCatalogAlert CreateAlert() => new()
    {
        Id = 42, UserId = "client", DetectedAtUtc = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc),
        UniqueSites = 5000, Threshold = 5000
    };
}
