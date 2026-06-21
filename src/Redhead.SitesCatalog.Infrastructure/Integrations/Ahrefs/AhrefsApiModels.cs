namespace Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

public sealed record AhrefsLimitsAndUsage(
    long UnitsLimitWorkspace,
    long UnitsUsageWorkspace,
    long UnitsLimitApiKey,
    long UnitsUsageApiKey,
    DateTime? UsageResetDate);

public sealed record AhrefsBatchTarget(string Url, string Mode, string Protocol);

public sealed record AhrefsBatchRow(
    int Index,
    long? OrganicTraffic,
    double? DomainRating,
    string? Error = null);

public sealed record AhrefsBatchCost(
    long? Rows,
    long? UnitsCostRow,
    long? UnitsCostTotal,
    long? UnitsCostTotalActual,
    string? Cache)
{
    public long EffectiveUnits => UnitsCostTotalActual ?? UnitsCostTotal ?? 0;
}

public sealed record AhrefsBatchResult(
    IReadOnlyList<AhrefsBatchRow> Rows,
    AhrefsBatchCost Cost);
