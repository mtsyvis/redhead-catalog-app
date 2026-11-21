using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public sealed class BulkOfferPricePreviewService(ApplicationDbContext context, OfferCurrencyService currencies)
{
    public async Task<BulkOfferPricePreviewResult> PreviewAsync(BulkOfferPricePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var contact = WebmasterPrimaryEmailRepairParser.Normalize(request.PrimaryEmail)!;
        var term = PricingTerm.FromTerm(request.TermType, request.TermValue, request.TermUnit);
        var types = request.PriceTypes.ToArray();
        var inputs = request.Rows.Select(row => new ParsedRow(row, DomainNormalizer.Normalize(row.Domain),
            row.Values.Select(ParseCell).ToArray())).ToList();
        var groups = inputs.GroupBy(row => DomainValidator.IsValidDnsDomain(row.Domain) ? row.Domain : row.Input.Domain,
            StringComparer.Ordinal).ToList();
        var domains = inputs.Where(row => DomainValidator.IsValidDnsDomain(row.Domain)).Select(row => row.Domain).Distinct().ToArray();
        var siteStates = await context.Sites.AsNoTracking().Where(site => domains.Contains(site.Domain))
            .OrderBy(site => site.Domain).Select(site => new { site.Domain, site.IsQuarantined, site.QuarantineUpdatedAtUtc })
            .ToListAsync(cancellationToken);
        var sites = siteStates.Select(site => site.Domain).ToHashSet(StringComparer.Ordinal);
        // Match current primary contact, never imported Contact text or one chosen WebmasterId.
        var offers = (await context.SiteWebmasterOffers.AsNoTracking().AsSplitQuery()
            .Where(offer => domains.Contains(offer.SiteDomain) && offer.Webmaster.PrimaryEmail == contact)
            .Include(offer => offer.Webmaster).Include(offer => offer.Prices).ToListAsync(cancellationToken)).ToLookup(offer => offer.SiteDomain);

        OfferCurrencyReference? reference = null;
        string? referenceError = null;
        var referenceResolved = false;
        var rows = new List<BulkOfferPricePreviewRow>();
        var duplicates = 0;
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = group.First();
            var originals = group.Select(row => row.Input).ToArray();
            var reasons = new List<string>();
            string? category = null;
            void Reject(string reasonCategory, string detail)
            {
                category ??= reasonCategory;
                reasons.Add(detail);
            }

            var distinctInputs = group.DistinctBy(row => string.Join("\u001f", row.Cells.Select(cell => cell.Key))).Count();
            duplicates += group.Count() - distinctInputs;
            if (distinctInputs > 1)
            {
                Reject("Conflicting input rows", "Repeated normalized domain has different proposed values; no row takes priority.");
            }

            var validDomain = DomainValidator.IsValidDnsDomain(first.Domain);
            if (!validDomain)
            {
                Reject("Invalid domain", "Invalid domain input; the original value is retained.");
            }

            foreach (var input in group)
            {
                if (input.Cells.Length != types.Length)
                {
                    Reject("Invalid selected value", $"Row {input.Input.RowNumber}: expected {types.Length} selected price cells, received {input.Cells.Length}.");
                }

                for (var index = 0; index < types.Length; index++)
                {
                    var cell = index < input.Cells.Length ? input.Cells[index] : ParseCell("");
                    if (cell.Status is null || (types[index] == WebmasterOfferPriceType.Main && cell.Amount is null))
                    {
                        Reject("Invalid selected value", $"Row {input.Input.RowNumber}, {types[index]}: enter a positive amount with at most two decimals within supported bounds"
                            + (types[index] == WebmasterOfferPriceType.Main ? "." : ", YES or NO.")
                            + $" Received '{(index < input.Input.Values.Count ? input.Input.Values[index] : "")}'.");
                    }
                }
            }

            SiteWebmasterOffer? candidate = null;
            PricingTerm? currentTerm = null;
            if (validDomain)
            {
                var domainOffers = offers[first.Domain].ToArray();
                if (!sites.Contains(first.Domain))
                {
                    Reject("Site not found", "The domain is absent from the catalog.");
                }
                else if (domainOffers.Length == 0)
                {
                    Reject("Offer not found", "This primary contact has no offer for the domain.");
                }
                else
                {
                    (candidate, currentTerm) = FindCandidate(domainOffers, term, Reject);
                }
            }

            var changes = new List<BulkOfferPriceChange>();
            if (candidate is not null && category is null)
            {
                for (var index = 0; index < types.Length; index++)
                {
                    var cell = first.Cells[index];
                    var saved = candidate.Prices.SingleOrDefault(price => price.PriceType == types[index]);
                    var current = saved is null ? null : BulkOfferPriceValue.FromPrice(saved);
                    BulkOfferPriceValue proposed;
                    if (cell.Amount is { } amount)
                    {
                        // Resubmitting an unchanged source quote must not reprice it at today's rate.
                        if (current?.AvailabilityStatus == ServiceAvailabilityStatus.Available &&
                            current.SourceCurrency == request.SourceCurrency && current.SourceAmount == amount)
                        {
                            proposed = current;
                        }
                        else
                        {
                            if (!referenceResolved)
                            {
                                referenceResolved = true;
                                try { reference = await currencies.GetReferenceAsync(request.SourceCurrency, cancellationToken: cancellationToken); }
                                catch (RequestValidationException error) { referenceError = error.Message; }
                            }
                            if (reference is null)
                            {
                                Reject("Conversion unavailable", $"{types[index]}: {referenceError}");
                                continue;
                            }
                            try
                            {
                                var conversion = reference.Convert(amount);
                                proposed = new(ServiceAvailabilityStatus.Available, conversion.WebmasterPriceUsd,
                                    conversion.SourceCurrency, conversion.SourceAmount, conversion.AppliedUsdRate, conversion.RateDate,
                                    conversion.AppliedUsdSurcharge);
                            }
                            catch (RequestValidationException error)
                            {
                                Reject("Invalid converted amount", $"{types[index]}: {error.Message}");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        proposed = new(cell.Status!.Value, null, current?.SourceCurrency ?? request.SourceCurrency, null, null, null);
                    }

                    changes.Add(new(types[index], current, proposed, current != proposed));
                }
            }
            var proposedStatus = candidate is null ? (SiteWebmasterOfferStatus?)null
                : first.Cells.Any(cell => cell.Amount.HasValue) ? SiteWebmasterOfferStatus.Active : candidate.Status;
            var outcome = category is not null ? BulkOfferPricePreviewOutcomes.Exceptions
                : changes.Any(change => change.Changed) || currentTerm != term || candidate!.Status != proposedStatus ? BulkOfferPricePreviewOutcomes.Ready : BulkOfferPricePreviewOutcomes.NoChanges;
            rows.Add(new(validDomain ? first.Domain : first.Input.Domain, !validDomain, originals, outcome, category,
                reasons.Distinct().ToArray(), candidate?.Id, currentTerm is null ? null : Label(currentTerm),
                candidate is null ? null : Label(term), candidate?.Status, proposedStatus, changes)
            {
                UnsubmittedPriceTypes = category is null && candidate is not null
                    ? candidate.Prices.Where(price => !types.Contains(price.PriceType)
                            && price.AvailabilityStatus == ServiceAvailabilityStatus.Available && price.WebmasterPriceUsd > 0)
                        .OrderBy(price => price.PriceType).Select(price => price.PriceType).ToArray()
                    : []
            });
        }

        // Recheck the same existing reference contract once, never silently mix provider snapshots.
        if (reference is not null)
        {
            await currencies.GetReferenceAsync(request.SourceCurrency, reference.RateReference, true, cancellationToken);
        }

        var result = new BulkOfferPricePreviewResult(contact, Label(term), request.SourceCurrency, inputs.Count, duplicates,
            rows.Count(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready), rows.Count(row => row.Outcome == BulkOfferPricePreviewOutcomes.NoChanges),
            rows.Count(row => row.Outcome == BulkOfferPricePreviewOutcomes.Exceptions), rows, reference);
        var readyDomains = rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready).Select(row => row.Domain).ToArray();
        if (readyDomains.Length > 0)
        {
            var suppliers = await context.SiteWebmasterOffers.AsNoTracking().AsSplitQuery()
                .Where(offer => readyDomains.Contains(offer.SiteDomain)).Include(offer => offer.Prices).Include(offer => offer.Site)
                .ToListAsync(cancellationToken);
            result = BulkOfferPurchaseMinima.Attach(result, BulkOfferPurchaseMinima.Preview(rows, suppliers, term));
        }
        // Bind Apply to the reviewed input, matching membership, xmin versions, site state and exact conversions.
        var version = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request, result, sites = siteStates,
            offers = offers.SelectMany(group => group).OrderBy(offer => offer.Id).Select(offer => new
            {
                offer.Id, offer.Version, offer.WebmasterId, WebmasterVersion = offer.Webmaster.Version,
                prices = offer.Prices.OrderBy(price => price.PriceType).Select(price => new
                {
                    price.PriceType, price.TermType, price.TermValue, price.TermUnit,
                    value = BulkOfferPriceValue.FromPrice(price), price.WebmasterPriceDetails, price.UpdatedAtUtc
                })
            })
        }));
        return result with { PreviewVersion = Convert.ToHexString(version) };
    }

    private static (SiteWebmasterOffer? Offer, PricingTerm? Term) FindCandidate(
        SiteWebmasterOffer[] domainOffers, PricingTerm term, Action<string, string> reject)
    {
        var valid = domainOffers.Select(offer => (Offer: offer, Term: GetTerm(offer.TermType, offer.TermValue, offer.TermUnit))).ToArray();
        if (valid.Any(entry => entry.Term is null))
        {
            reject("Invalid structured term", "An offer for this contact/domain has a malformed structured term.");
        }

        var eligible = valid.Where(entry => entry.Term is not null &&
            (entry.Term == term || entry.Term == PricingTerm.Unknown)).ToArray();
        if (eligible.Length > 1)
        {
            reject("Multiple matching offers", $"{eligible.Length} offers match the selected term or No term, including Active and Inactive offers.");
        }
        else if (eligible.Length == 0 && valid.All(entry => entry.Term is not null))
        {
            reject("Different term", "This contact has offers only with another known term.");
        }
        else if (eligible.Length == 1)
        {
            var candidate = eligible[0].Offer;
            var currentTerm = eligible[0].Term;
            if (candidate.Prices.Any(price => GetTerm(price.TermType, price.TermValue, price.TermUnit) != currentTerm))
            {
                reject("Inconsistent price terms", "Every existing price row, including unselected types, must have a valid structured term equal to the offer term.");
            }
            return (candidate, currentTerm);
        }
        return (null, null);
    }

    internal static void ValidateRequest(BulkOfferPricePreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PrimaryEmail) || request.PrimaryEmail.Length > WebmasterOfferFieldLimits.PrimaryEmailMaxLength)
        {
            throw new RequestValidationException($"A primary email/contact value of at most {WebmasterOfferFieldLimits.PrimaryEmailMaxLength:N0} characters is required.");
        }

        if (GetTerm(request.TermType, request.TermValue, request.TermUnit) is null)
        {
            throw new RequestValidationException("Price-list term must be No term, Permanent, or positive finite years.");
        }

        if (!CurrencyCodeValidator.IsValid(request.SourceCurrency))
        {
            throw new RequestValidationException("Currency must be an uppercase three-letter code.");
        }

        if (request.PriceTypes is null || request.PriceTypes.Count == 0 || request.PriceTypes.Any(type => !Enum.IsDefined(type)) ||
            request.PriceTypes.Distinct().Count() != request.PriceTypes.Count)
        {
            throw new RequestValidationException("Select one or more unique supported price types.");
        }

        if (request.Rows is null || request.Rows.Count == 0 || request.Rows.Count > MultiSearchConstants.WebmasterOffersMaxInputs)
        {
            throw new RequestValidationException($"Enter between 1 and {MultiSearchConstants.WebmasterOffersMaxInputs:N0} input rows.");
        }

        if (request.Rows.Any(row => row is null || row.RowNumber <= 0 || row.Domain is null || row.Values is null || row.Values.Any(value => value is null)))
        {
            throw new RequestValidationException("Each input row must include its row number, domain and selected cells.");
        }
    }

    private static ParsedCell ParseCell(string value)
    {
        var text = value.Trim().ToUpperInvariant();
        if (text == "YES")
        {
            return new(ServiceAvailabilityStatus.AvailableWithUnknownPrice, null, "YES");
        }

        if (text == "NO")
        {
            return new(ServiceAvailabilityStatus.NotAvailable, null, "NO");
        }

        if (DecimalParsingHelper.TryParseDecimalFlexible(value, out var amount) && OfferCurrencyService.IsValidAmount(amount))
        {
            return new(ServiceAvailabilityStatus.Available, amount, amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
        }

        return new(null, null, "invalid:" + text);
    }

    internal static PricingTerm? GetTerm(TermType? type, int? value, TermUnit? unit)
    {
        var term = PricingTerm.FromTerm(type, value, unit);
        return PricingTerm.TryCreate(term.TermKey, type, value, unit, out var validTerm) ? validTerm : null;
    }

    private static string Label(PricingTerm term) => term == PricingTerm.Unknown ? "No term" : term.DisplayLabel;
    private sealed record ParsedRow(BulkOfferPriceInputRow Input, string Domain, ParsedCell[] Cells);
    private sealed record ParsedCell(ServiceAvailabilityStatus? Status, decimal? Amount, string Key);
}
