namespace Redhead.SitesCatalog.Application.Models.Exports;

public sealed record ExportUsageLimitEvaluation(
    bool Applies,
    bool IsBlocked,
    string? BlockedReason,
    string? TruncationReason,
    IReadOnlyList<string> AllowedDomains,
    ExportUsageSummary Usage);
