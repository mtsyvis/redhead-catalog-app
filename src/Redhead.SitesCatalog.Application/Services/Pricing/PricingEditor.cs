using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.Pricing;

public sealed class PricingEditor(ApplicationDbContext context, ISitesCatalogCache cache, TimeProvider clock)
{
    public async Task SaveAsync(string domain, SavePricingRequest request, string? email, CancellationToken cancellationToken)
    {
        var validation = SiteWriteValidator.ValidatePricing(request.Pricing);
        if (!validation.IsValid || validation.NormalizedPricing is not { } pricing)
        {
            throw new RequestValidationException(string.Join(" ", validation.FieldErrors.Values.SelectMany(errors => errors)));
        }
        // Serializable protects the read/check/write sequence, including child price rows.
        // A concurrent writer must cause a conflict rather than silently losing prices.
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        try
        {
            var normalized = DomainNormalizer.Normalize(domain);
            var site = await context.Sites.Include(row => row.PriceOptions).Include(row => row.ServiceAvailabilities)
                .SingleOrDefaultAsync(row => row.Domain == normalized, cancellationToken)
                ?? throw new KeyNotFoundException("Site was not found.");
            var original = SitePricingUpdater.Snapshot(site);
            var originalVersion = SitePricingUpdater.Version(original);
            if (request.Version != originalVersion)
            {
                throw new PricingConflictException();
            }
            var terms = original.Prices.Select(price => price.TermKey).ToHashSet(StringComparer.Ordinal);
            if (terms.Count == 0) terms.Add(PricingTerm.UnknownKey);
            if (original.Prices.Any(price => !PricingTerm.TryCreate(price.TermKey, price.TermType, price.TermValue, price.TermUnit, out _)) ||
                pricing.Prices.Any(price => !terms.Contains(price.TermKey)))
            {
                throw new RequestValidationException("Change or repair placement terms in the full site editor before editing these prices.");
            }
            if (originalVersion == SitePricingUpdater.Version(pricing))
            {
                return;
            }

            var before = EntityChangeHistoryRecorder.CaptureSite(site);
            var now = clock.GetUtcNow().UtcDateTime;
            SitePricingUpdater.Apply(context, site, pricing, now);
            site.UpdatedAtUtc = now;
            site.UpdatedBy = AuditUserFormatter.Format(email);
            EntityChangeHistoryRecorder.RecordUpdate(context, EntityChangeHistoryConstants.SiteEntityType, site.Domain,
                before, EntityChangeHistoryRecorder.CaptureSite(site), EntityChangeHistoryConstants.ManualSource, email, now);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            cache.InvalidateTermOptions();
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException ||
            exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
        {
            throw new PricingConflictException();
        }
    }
}
