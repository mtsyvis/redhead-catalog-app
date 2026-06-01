namespace Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;

public enum WeeklyEmergencySitesExportJobStatus
{
    NotDue,
    AlreadyCompleted,
    AlreadyRunning,
    Completed,
    Failed
}

public sealed record WeeklyEmergencySitesExportJobResult(
    WeeklyEmergencySitesExportJobStatus Status,
    string? PeriodKey = null,
    DateTime? DueOccurrenceUtc = null)
{
    public static WeeklyEmergencySitesExportJobResult NotDue()
        => new(WeeklyEmergencySitesExportJobStatus.NotDue);

    public static WeeklyEmergencySitesExportJobResult AlreadyCompleted(
        string periodKey,
        DateTime dueOccurrenceUtc)
        => new(WeeklyEmergencySitesExportJobStatus.AlreadyCompleted, periodKey, dueOccurrenceUtc);

    public static WeeklyEmergencySitesExportJobResult AlreadyRunning(
        string periodKey,
        DateTime dueOccurrenceUtc)
        => new(WeeklyEmergencySitesExportJobStatus.AlreadyRunning, periodKey, dueOccurrenceUtc);

    public static WeeklyEmergencySitesExportJobResult Completed(
        string periodKey,
        DateTime dueOccurrenceUtc)
        => new(WeeklyEmergencySitesExportJobStatus.Completed, periodKey, dueOccurrenceUtc);

    public static WeeklyEmergencySitesExportJobResult Failed(
        string periodKey,
        DateTime dueOccurrenceUtc)
        => new(WeeklyEmergencySitesExportJobStatus.Failed, periodKey, dueOccurrenceUtc);
}
