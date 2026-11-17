using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public sealed class BulkOfferPriceConflictException() : Exception(
    "Offers, contact membership, site state or currency rates changed. Refresh the preview before applying; no updates were saved.");

public sealed class BulkOfferPriceOperationService(ApplicationDbContext context, BulkOfferPricePreviewService previews)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BulkOfferPriceOperationResult?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var saved = await context.BulkOfferPriceOperations.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return saved is null ? null : Read(saved);
    }

    public async Task<IReadOnlyList<BulkOfferPriceOperationSummary>> RecentAsync(CancellationToken cancellationToken = default)
        => await context.Database.SqlQueryRaw<BulkOfferPriceOperationSummary>("""
            SELECT "Id", "Actor", "AppliedAtUtc", "PrimaryEmail", "UpdatedCount",
                ("SnapshotJson" -> 'operation' -> 'result' ->> 'noChangesCount')::integer AS "NoChangesCount",
                ("SnapshotJson" -> 'operation' -> 'result' ->> 'exceptionCount')::integer AS "ExceptionCount"
            FROM "BulkOfferPriceOperations"
            ORDER BY "AppliedAtUtc" DESC, "Id" DESC
            LIMIT 10
            """).ToListAsync(cancellationToken);

    public async Task<BulkOfferPriceOperationResult> ApplyAsync(BulkOfferPriceApplyRequest request, string? actor,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty || request.Input is null || string.IsNullOrWhiteSpace(request.PreviewVersion))
        {
            throw new RequestValidationException("An operation retry identity, input and checked preview are required.");
        }
        BulkOfferPricePreviewService.ValidateRequest(request.Input);

        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize retries, including two requests sent before either has committed.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({request.OperationId.ToString("D")}, 0))", cancellationToken);
            var existing = await context.BulkOfferPriceOperations.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestHash != hash)
                {
                    throw new RequestValidationException("This retry identity belongs to a different request.");
                }
                return Read(existing);
            }

            await LockScopeAsync(request.Input, cancellationToken);

            var preview = await previews.PreviewAsync(request.Input, cancellationToken);
            if (preview.PreviewVersion != request.PreviewVersion)
            {
                throw new BulkOfferPriceConflictException();
            }
            if (preview.ReadyCount == 0)
            {
                throw new RequestValidationException("There are no updates to apply.");
            }

            var ready = preview.Rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready).ToArray();
            var readyDomains = ready.Select(row => row.Domain).ToArray();
            var suppliers = await context.SiteWebmasterOffers.Where(offer => readyDomains.Contains(offer.SiteDomain))
                .Include(offer => offer.Webmaster).Include(offer => offer.Prices).Include(offer => offer.Site)
                .Include(offer => offer.LinkbuilderMailboxes).ThenInclude(link => link.LinkbuilderMailbox)
                .AsSplitQuery().ToListAsync(cancellationToken);
            var byId = suppliers.ToDictionary(offer => offer.Id);
            var beforePrices = ready.ToDictionary(row => row.OfferId!.Value, row => CapturePrices(byId[row.OfferId!.Value]));
            var term = PricingTerm.FromTerm(request.Input.TermType, request.Input.TermValue, request.Input.TermUnit);
            var keys = BulkOfferPurchaseMinima.AffectedKeys(ready, byId, term);
            var suppliersByDomain = suppliers.ToLookup(offer => offer.SiteDomain);
            var beforeMinima = keys.ToDictionary(key => key, key => BulkOfferPurchaseMinima.Minimum(suppliersByDomain[key.Domain], key));
            var now = DateTime.UtcNow;
            var actorName = AuditUserFormatter.Format(actor);
            foreach (var row in ready)
            {
                var offer = byId[row.OfferId!.Value];
                var before = CaptureHistory(offer);
                Patch(offer, row, term, now);
                var after = CaptureHistory(offer);
                after["Bulk operation"] = request.OperationId.ToString("D");
                EntityChangeHistoryRecorder.RecordUpdate(context, EntityChangeHistoryConstants.WebmasterOfferEntityType,
                    offer.Id.ToString("D"), before, after, "Bulk price update", actor, now);
                offer.UpdatedAtUtc = now;
                offer.UpdatedBy = actorName;
                // Parent xmin changes even for child-only patches.
                context.Entry(offer).Property(value => value.UpdatedAtUtc).IsModified = true;
            }
            var result = new BulkOfferPriceOperationResult(request.OperationId, actorName, now, request.Input.PriceTypes, preview);
            var snapshot = CreateSnapshot(result, request.Input, beforePrices, beforeMinima, byId, suppliersByDomain);
            result = result with { Result = BulkOfferPurchaseMinima.Attach(result.Result, snapshot.Minima) };
            snapshot = snapshot with { Operation = result };
            context.BulkOfferPriceOperations.Add(new BulkOfferPriceOperation
            {
                Id = result.Id, Actor = actorName, AppliedAtUtc = now, PrimaryEmail = preview.PrimaryEmail,
                UpdatedCount = preview.ReadyCount, RequestHash = hash, SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions)
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception error) when (error is DbUpdateConcurrencyException ||
            (error is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected) ||
            (error is DbUpdateException { InnerException: PostgresException inner } &&
                inner.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw new BulkOfferPriceConflictException();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task LockScopeAsync(BulkOfferPricePreviewRequest input, CancellationToken cancellationToken)
    {
        // Site FOR UPDATE also blocks FK inserts of new offers. Offer locks protect child inserts,
        // updates/deletes and quarantine deactivation; webmaster locks protect contact membership.
        // Read committed is deliberate: after waiting for locks, revalidation sees committed changes.
        // Lock all suppliers for these domains so both purchase snapshots describe the committed batch.
        var domains = input.Rows.Select(row => DomainNormalizer.Normalize(row.Domain))
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Sites\" WHERE \"Domain\" = ANY({domains}) ORDER BY \"Domain\" FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"SiteWebmasterOffers\" WHERE \"SiteDomain\" = ANY({domains}) ORDER BY \"Id\" FOR UPDATE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Webmasters\" WHERE \"Id\" IN (SELECT \"WebmasterId\" FROM \"SiteWebmasterOffers\" WHERE \"SiteDomain\" = ANY({domains})) ORDER BY \"Id\" FOR SHARE", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"WebmasterOfferPrices\" WHERE \"SiteWebmasterOfferId\" IN (SELECT \"Id\" FROM \"SiteWebmasterOffers\" WHERE \"SiteDomain\" = ANY({domains})) ORDER BY \"Id\" FOR UPDATE", cancellationToken);
        // Refresh replaces the singleton table. Keep the reviewed reference stable until commit.
        await context.Database.ExecuteSqlRawAsync("LOCK TABLE \"CurrencyReferenceSnapshots\" IN SHARE MODE", cancellationToken);
    }

    private static BulkOfferPriceOperationSnapshot CreateSnapshot(BulkOfferPriceOperationResult result,
        BulkOfferPricePreviewRequest input, IReadOnlyDictionary<Guid, BulkOfferSavedPrice[]> beforePrices,
        IReadOnlyDictionary<BulkOfferPurchaseMinima.Key, decimal?> beforeMinima, IReadOnlyDictionary<Guid, SiteWebmasterOffer> byId,
        ILookup<string, SiteWebmasterOffer> byDomain)
    {
        var offers = beforePrices.Select(entry => new BulkOfferSavedOffer(entry.Key, entry.Value,
            CapturePrices(byId[entry.Key]))).ToArray();
        var minima = beforeMinima.Keys.OrderBy(key => key.Domain, StringComparer.Ordinal)
            .ThenBy(key => key.Service).ThenBy(key => key.Term.TermKey, StringComparer.Ordinal)
            .Select(key => new BulkOfferPurchaseMinimum(key.Domain, key.Service, key.Term, beforeMinima[key],
                BulkOfferPurchaseMinima.Minimum(byDomain[key.Domain], key), byDomain[key.Domain].First().Site.IsQuarantined)).ToArray();
        return new(result, input, offers, minima);
    }

    private void Patch(SiteWebmasterOffer offer, BulkOfferPricePreviewRow row, PricingTerm term, DateTime now)
    {
        if (row.CurrentTerm != row.ProposedTerm)
        {
            offer.TermType = term.TermType;
            offer.TermValue = term.TermValue;
            offer.TermUnit = term.TermUnit;
            foreach (var price in offer.Prices)
            {
                SetTerm(price, term);
                price.UpdatedAtUtc = now;
            }
        }
        offer.Status = row.ProposedStatus!.Value;
        foreach (var change in row.Prices.Where(change => change.Changed))
        {
            var price = offer.Prices.SingleOrDefault(price => price.PriceType == change.PriceType);
            if (price is null)
            {
                price = new WebmasterOfferPrice { Id = Guid.NewGuid(), SiteWebmasterOfferId = offer.Id,
                    PriceType = change.PriceType, CreatedAtUtc = now };
                SetTerm(price, term);
                offer.Prices.Add(price);
                context.WebmasterOfferPrices.Add(price);
            }
            var value = change.Proposed;
            price.AvailabilityStatus = value.AvailabilityStatus;
            price.WebmasterPriceUsd = value.WebmasterPriceUsd;
            price.SourceCurrency = value.SourceCurrency;
            price.SourceAmount = value.SourceAmount;
            price.AppliedUsdRate = value.AppliedUsdRate;
            price.AppliedUsdSurcharge = value.AppliedUsdSurcharge;
            price.RateDate = value.RateDate;
            price.UpdatedAtUtc = now;
        }
    }

    private static void SetTerm(WebmasterOfferPrice price, PricingTerm term)
        => (price.TermType, price.TermValue, price.TermUnit) = (term.TermType, term.TermValue, term.TermUnit);

    private static BulkOfferSavedPrice[] CapturePrices(SiteWebmasterOffer offer)
        => offer.Prices.OrderBy(price => price.PriceType).Select(price => new BulkOfferSavedPrice(price.PriceType,
            BulkOfferPricePreviewService.GetTerm(price.TermType, price.TermValue, price.TermUnit)!,
            BulkOfferPriceValue.FromPrice(price), price.WebmasterPriceDetails)).ToArray();

    private static SortedDictionary<string, string?> CaptureHistory(SiteWebmasterOffer offer)
    {
        var values = new SortedDictionary<string, string?>(EntityChangeHistoryRecorder.CaptureWebmasterOffer(offer)
            .ToDictionary(entry => entry.Key, entry => entry.Value), StringComparer.Ordinal);
        foreach (var price in offer.Prices)
        {
            values[$"Price · {price.PriceType} · term"] = BulkOfferPricePreviewService.GetTerm(price.TermType, price.TermValue, price.TermUnit)?.DisplayLabel;
            values[$"Price · {price.PriceType} · rate date"] = price.RateDate?.ToString("yyyy-MM-dd");
        }
        return values;
    }

    public async Task<byte[]?> DownloadAsync(Guid id, bool calculation, CancellationToken cancellationToken = default)
        => (await PrepareReportAsync(id, calculation, cancellationToken))?.Content;

    public async Task<BulkOfferPriceReport?> PrepareReportAsync(Guid id, bool calculation, CancellationToken cancellationToken = default)
    {
        var json = await context.BulkOfferPriceOperations.AsNoTracking().Where(row => row.Id == id)
            .Select(row => row.SnapshotJson).SingleOrDefaultAsync(cancellationToken);
        if (json is null)
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<BulkOfferPriceOperationSnapshot>(json, JsonOptions)!;
        return calculation ? BulkOfferPriceReports.Calculation(snapshot) : BulkOfferPriceReports.Update(snapshot);
    }

    private static BulkOfferPriceOperationResult Read(BulkOfferPriceOperation saved)
    {
        var result = JsonSerializer.Deserialize<StoredResult>(saved.SnapshotJson, JsonOptions)!.Operation;
        if (result.Result.Rows.Any(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready && row.PurchaseMinima is null))
        {
            // Slice 2 results already have factual minima. Read only those needed for display;
            // never deserialize Input/Offers or query live suppliers to reopen a historical result.
            var minima = JsonSerializer.Deserialize<StoredMinima>(saved.SnapshotJson, JsonOptions)!.Minima;
            result = result with { Result = BulkOfferPurchaseMinima.Attach(result.Result, minima) };
        }
        return result;
    }

    // Skip input, price snapshots and minima when only the UI result is requested.
    private sealed record StoredResult(BulkOfferPriceOperationResult Operation);
    private sealed record StoredMinima(IReadOnlyList<BulkOfferPurchaseMinimum> Minima);
}
