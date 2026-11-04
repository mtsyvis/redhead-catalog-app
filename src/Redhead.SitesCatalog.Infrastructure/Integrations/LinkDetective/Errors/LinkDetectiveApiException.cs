using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;

public sealed class LinkDetectiveApiException(
    string message,
    DateTime? retryAfterUtc = null,
    LinkDetectiveFailureKind kind = LinkDetectiveFailureKind.Transient) : Exception(message)
{
    public DateTime? RetryAfterUtc { get; } = retryAfterUtc;
    public LinkDetectiveFailureKind Kind { get; } = kind;

    internal static LinkDetectiveApiException IncompleteResponse() => new(LinkDetectiveMessages.IncompletePrices);
}
