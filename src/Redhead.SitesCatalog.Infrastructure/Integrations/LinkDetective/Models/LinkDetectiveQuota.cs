namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

// ResetAtUtc is normalized when the provider response is read; an unknown reset remains null.
public sealed record LinkDetectiveQuota(long Limit, long Used, long Remaining, string Period, DateTime? ResetAtUtc);
