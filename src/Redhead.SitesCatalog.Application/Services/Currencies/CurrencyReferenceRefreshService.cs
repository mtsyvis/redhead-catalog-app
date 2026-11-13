using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Integrations.Currencies;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Services.Currencies;

public sealed class CurrencyReferenceRefreshService(ApplicationDbContext context, ICurrencyReferenceRateClient client,
    IOptions<ExchangeRateApiOptions> options, TimeProvider clock)
{
    private const int RefreshIntervalHours = 24;

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            return;
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var state = await context.CurrencyReferenceRefreshStates.SingleOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new CurrencyReferenceRefreshState();
            context.CurrencyReferenceRefreshStates.Add(state);
        }
        if (state.NextAttemptAtUtc > now)
        {
            return;
        }

        state.LastAttemptAtUtc = now;
        state.LastAttemptSucceeded = false;
        state.NextAttemptAtUtc = now.AddHours(RefreshIntervalHours);
        // Save before the HTTP request so a restart does not repeat this attempt.
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var table = await client.FetchAsync(cancellationToken);
            // Save the replacement and removal together, so readers always see one complete table.
            context.CurrencyReferenceSnapshots.RemoveRange(
                await context.CurrencyReferenceSnapshots.ToListAsync(cancellationToken));
            context.CurrencyReferenceSnapshots.Add(new CurrencyReferenceSnapshot
            {
                Id = Guid.NewGuid(), ProviderAsOfUtc = table.ProviderAsOfUtc, FetchedAtUtc = clock.GetUtcNow().UtcDateTime,
                ProviderNextUpdateUtc = table.ProviderNextUpdateUtc, Provider = ExchangeRateApiClient.ProviderName,
                UsdRatesJson = JsonSerializer.Serialize(table.UsdRates)
            });
            state.LastAttemptSucceeded = true;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or FormatException or TaskCanceledException)
        {
            // Retry after the refresh interval; preserve last-good snapshots. No provider text is logged.
        }
    }
}
