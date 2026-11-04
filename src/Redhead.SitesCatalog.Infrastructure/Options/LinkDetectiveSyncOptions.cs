using System.Globalization;

namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class LinkDetectiveSyncOptions
{
    public const string SectionName = "LinkDetectiveSync";
    public bool Enabled { get; set; }
    public string DailySelectionTimeUtc { get; set; } = "00:10";
    public int BatchSize { get; set; } = 500;
    public int OnDemandReserve { get; set; } = 10000;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;

    public static bool IsValid(LinkDetectiveSyncOptions options) =>
        options.BatchSize > 0 && options.OnDemandReserve >= 0 &&
        options.MaxAttempts is >= 1 and <= 10 && options.RetryDelayMinutes is >= 1 and <= 1440 &&
        TimeOnly.TryParseExact(options.DailySelectionTimeUtc, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public DateTime NextSelection(DateTime now)
    {
        var next = SelectionOn(now);
        return next > now ? next : next.AddDays(1);
    }

    public DateTime SelectionOn(DateTime day) => day.Date.Add(
        TimeOnly.ParseExact(DailySelectionTimeUtc, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan());
}
