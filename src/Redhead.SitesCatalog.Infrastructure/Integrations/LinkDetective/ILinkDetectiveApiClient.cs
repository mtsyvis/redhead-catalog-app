namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;

public interface ILinkDetectiveApiClient
{
    Task<LinkDetectiveDomainPrices> GetPricesAsync(string domain, CancellationToken cancellationToken);
}

public sealed record LinkDetectiveDomainPrices(decimal? MinimumUsd, decimal? AverageUsd, decimal? MedianUsd);

public sealed class LinkDetectiveApiException(string message, DateTime? retryAfterUtc = null) : Exception(message)
{
    public DateTime? RetryAfterUtc { get; } = retryAfterUtc;
}
