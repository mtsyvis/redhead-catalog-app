using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.LinkDetective;

// Mutates item state and matching run counters together; does not access the database.
internal static class LinkDetectiveSyncProgress
{
    public static void RetryOrFail(
        LinkDetectiveSyncRun run, LinkDetectiveSyncRunItem item, string error, DateTime now, LinkDetectiveSyncOptions options)
    {
        item.Error = error;
        item.Status = LinkDetectiveSyncItemStatus.Pending;
        item.NextAttemptAtUtc = now.AddMinutes(options.RetryDelayMinutes);
        if (item.Attempts >= options.MaxAttempts)
        {
            Complete(run, item, LinkDetectiveSyncItemStatus.Failed);
        }
    }

    public static void Complete(LinkDetectiveSyncRun run, LinkDetectiveSyncRunItem item, LinkDetectiveSyncItemStatus status)
    {
        item.Status = status;
        item.NextAttemptAtUtc = null;
        if (status != LinkDetectiveSyncItemStatus.Failed)
        {
            item.Error = null;
        }

        run.ProcessedCount++;
        switch (status)
        {
            case LinkDetectiveSyncItemStatus.PricesFound: run.PricesFoundCount++; break;
            case LinkDetectiveSyncItemStatus.NotFound: run.NotFoundCount++; break;
            case LinkDetectiveSyncItemStatus.Failed: run.FailedCount++; break;
            case LinkDetectiveSyncItemStatus.Reused: run.ReusedCount++; break;
            case LinkDetectiveSyncItemStatus.Skipped: run.SkippedCount++; break;
        }
    }
}
