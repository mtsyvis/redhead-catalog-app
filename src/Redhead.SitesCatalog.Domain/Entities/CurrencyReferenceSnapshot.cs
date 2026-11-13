namespace Redhead.SitesCatalog.Domain.Entities;

// The current table only. Its ID is the temporary preview version; refresh replaces the row.
public sealed class CurrencyReferenceSnapshot
{
    public Guid Id { get; set; }
    public DateTime ProviderAsOfUtc { get; set; }
    public DateTime? ProviderNextUpdateUtc { get; set; }
    public string Provider { get; set; } = "ExchangeRateAPI";
    public DateTime FetchedAtUtc { get; set; }
    public string UsdRatesJson { get; set; } = "{}";
}

public sealed class CurrencyReferenceRefreshState
{
    public int Id { get; set; } = 1;
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public bool LastAttemptSucceeded { get; set; }
}
