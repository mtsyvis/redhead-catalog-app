namespace Redhead.SitesCatalog.Domain.Exceptions;

public sealed class ClientCatalogBurstLimitExceededException(DateTimeOffset retryAtUtc, DateTimeOffset nowUtc)
    : Exception($"This selection exceeds your five-minute catalog limit. Try again in {Math.Max(1, (int)Math.Ceiling((retryAtUtc - nowUtc).TotalSeconds))} seconds or narrow your filters. Your results have been preserved.")
{
    public int RetryAfterSeconds { get; } = Math.Max(1, (int)Math.Ceiling((retryAtUtc - nowUtc).TotalSeconds));
}
