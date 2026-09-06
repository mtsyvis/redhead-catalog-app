using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Invitations;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Application.Invitations;

public sealed class InvitationDeliveryServiceTests
{
    [Fact]
    public async Task DeliverAsync_BuildsTrustedUrlAndReturnsSenderOutcome()
    {
        // Arrange
        var sender = new StubInvitationEmailSender(
            new InvitationEmailSendResult(InvitationEmailSendStatus.Sent));
        var logger = new RecordingLogger<InvitationDeliveryService>();
        var sut = CreateService(sender, logger);
        var request = new InvitationDeliveryRequest(
            "user-1",
            "person@example.com",
            "/activate-account?token=secret-token",
            DateTime.UtcNow.AddHours(48),
            InvitationEventType.Create);

        // Act
        var result = await sut.DeliverAsync(request);

        // Assert
        Assert.Equal(
            "https://catalog.rhda.us/activate-account?token=secret-token",
            result.ActivationUrl);
        Assert.Equal(InvitationEmailSendStatus.Sent, result.EmailDeliveryStatus);
        var sendRequest = Assert.Single(sender.Requests);
        Assert.Equal(request.RecipientEmail, sendRequest.RecipientEmail);
        Assert.Equal(result.ActivationUrl, sendRequest.ActivationUrl);
        Assert.Equal(request.InvitationExpiresAtUtc, sendRequest.InvitationExpiresAtUtc);
    }

    [Fact]
    public async Task DeliverAsync_WhenSenderThrows_ReturnsFailedAndLogsOnlySafeContext()
    {
        // Arrange
        var sender = new StubInvitationEmailSender(new InvalidOperationException("raw SMTP response"));
        var logger = new RecordingLogger<InvitationDeliveryService>();
        var sut = CreateService(sender, logger);
        var request = new InvitationDeliveryRequest(
            "user-42",
            "person@example.com",
            "/activate-account?token=secret-token",
            DateTime.UtcNow.AddHours(48),
            InvitationEventType.Reissue);

        // Act
        var result = await sut.DeliverAsync(request);

        // Assert
        Assert.Equal(InvitationEmailSendStatus.Failed, result.EmailDeliveryStatus);
        var log = Assert.Single(logger.Messages);
        Assert.Contains("user-42", log);
        Assert.Contains("Reissue", log);
        Assert.Contains("p***@example.com", log);
        Assert.DoesNotContain("person@example.com", log);
        Assert.DoesNotContain("secret-token", log);
        Assert.DoesNotContain("raw SMTP response", log);
    }

    private static InvitationDeliveryService CreateService(
        IInvitationEmailSender sender,
        ILogger<InvitationDeliveryService> logger)
        => new(
            sender,
            Options.Create(new FrontendOptions { BaseUrl = "https://catalog.rhda.us" }),
            logger);

    private sealed class StubInvitationEmailSender : IInvitationEmailSender
    {
        private readonly InvitationEmailSendResult? _result;
        private readonly Exception? _exception;

        public StubInvitationEmailSender(InvitationEmailSendResult result)
        {
            _result = result;
        }

        public StubInvitationEmailSender(Exception exception)
        {
            _exception = exception;
        }

        public List<InvitationEmailSendRequest> Requests { get; } = [];

        public Task<InvitationEmailSendResult> SendAsync(
            InvitationEmailSendRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return _exception == null
                ? Task.FromResult(_result!)
                : Task.FromException<InvitationEmailSendResult>(_exception);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
