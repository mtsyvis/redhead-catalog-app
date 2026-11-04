using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Persistence;

public static class LinkDetectiveQuotaPersistence
{
    private const int SingletonStateId = 1;

    // Callers hold the shared refresh lock and commit quota together with their cache/run changes.
    public static async Task<LinkDetectiveSyncState> GetOrCreateStateAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var state = await context.LinkDetectiveSyncStates.FindAsync([SingletonStateId], cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new LinkDetectiveSyncState { Id = SingletonStateId };
        context.LinkDetectiveSyncStates.Add(state);
        return state;
    }

    public static void ApplyObservation(LinkDetectiveSyncState state, LinkDetectiveQuota quota, DateTime now)
    {
        state.QuotaLimit = quota.Limit;
        state.QuotaUsed = quota.Used;
        state.QuotaRemaining = quota.Remaining;
        state.QuotaPeriod = quota.Period;
        state.QuotaResetAtUtc = quota.ResetAtUtc > now ? quota.ResetAtUtc : null;
        state.QuotaObservedAtUtc = now;
    }
}
