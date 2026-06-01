namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class EmergencySitesExportOptions
{
    public const string SectionName = "EmergencySitesExport";
    public const string DefaultScheduleCron = "30 3 * * MON";
    public const string DefaultServiceAccountJsonPath = "/run/secrets/google-service-account.json";
    public const string DefaultFilePrefix = "redhead-sites-full";
    public const int DefaultRetentionWeeks = 8;
    public const int DefaultUploadTimeoutMinutes = 30;

    public bool Enabled { get; set; }
    public string ScheduleCron { get; set; } = DefaultScheduleCron;
    public string? GoogleDriveFolderId { get; set; }
    public string ServiceAccountJsonPath { get; set; } = DefaultServiceAccountJsonPath;
    public int RetentionWeeks { get; set; } = DefaultRetentionWeeks;
    public string FilePrefix { get; set; } = DefaultFilePrefix;
    public int UploadTimeoutMinutes { get; set; } = DefaultUploadTimeoutMinutes;

    public static bool IsValid(EmergencySitesExportOptions options)
        => !options.Enabled ||
            (!string.IsNullOrWhiteSpace(options.ScheduleCron) &&
             !string.IsNullOrWhiteSpace(options.GoogleDriveFolderId) &&
             !string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath) &&
             !string.IsNullOrWhiteSpace(options.FilePrefix) &&
             options.RetentionWeeks > 0 &&
             options.UploadTimeoutMinutes > 0);
}
