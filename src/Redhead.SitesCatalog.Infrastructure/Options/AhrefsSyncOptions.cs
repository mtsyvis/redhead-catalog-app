namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class AhrefsSyncOptions
{
    public const string SectionName = "AhrefsSync";
    public const string DefaultCron = "0 1 1 * *";

    public bool Enabled { get; set; } = true;
    public string Cron { get; set; } = DefaultCron;
    public DateTimeOffset? NotBeforeUtc { get; set; }
    public int BatchSize { get; set; } = 100;
    public int MaxSitesPerRun { get; set; } = 100000;
    public string TargetMode { get; set; } = "subdomains";
    public string Protocol { get; set; } = "both";
    public string VolumeMode { get; set; } = "monthly";
    public int MonthlyAppBudgetUnits { get; set; } = 975000;
    public int SafetyBufferUnits { get; set; } = 25000;

    public static bool IsValid(AhrefsSyncOptions options)
        => !string.IsNullOrWhiteSpace(options.Cron) &&
            options.BatchSize is > 0 and <= 100 &&
            options.MaxSitesPerRun > 0 &&
            string.Equals(options.TargetMode, "subdomains", StringComparison.Ordinal) &&
            string.Equals(options.Protocol, "both", StringComparison.Ordinal) &&
            string.Equals(options.VolumeMode, "monthly", StringComparison.Ordinal) &&
            options.MonthlyAppBudgetUnits > 0 &&
            options.SafetyBufferUnits >= 0;
}
