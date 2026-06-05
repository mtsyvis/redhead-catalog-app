namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class ExportedDomainAccessCleanupOptions
{
    public const string SectionName = "ExportedDomainAccessCleanup";
    public const int DefaultRetentionDays = 30;
    public const int DefaultBatchSize = 1000;
    public const int DefaultIntervalHours = 24;
    public const int MinimumRetentionDays = 7;

    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = DefaultRetentionDays;
    public int BatchSize { get; set; } = DefaultBatchSize;
    public int IntervalHours { get; set; } = DefaultIntervalHours;

    public static bool IsValid(ExportedDomainAccessCleanupOptions options)
        => !options.Enabled ||
            (options.RetentionDays >= MinimumRetentionDays &&
             options.BatchSize > 0 &&
             options.IntervalHours > 0);
}
