using Microsoft.EntityFrameworkCore;
using Npgsql;
using Redhead.SitesCatalog.Application.Audit;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.ChangeHistory;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

public sealed class WebmasterOffersService : IWebmasterOffersService
{
    private readonly ApplicationDbContext _context;

    public WebmasterOffersService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WebmasterOffersSearchResult> GetByDomainAsync(string? domain, CancellationToken cancellationToken = default)
    {
        var normalizedDomain = DomainNormalizer.Normalize(domain);
        if (string.IsNullOrEmpty(normalizedDomain))
        {
            return new WebmasterOffersSearchResult
            {
                Domain = string.Empty,
                SiteFound = false,
                Offers = []
            };
        }

        var siteFound = await _context.Sites
            .AsNoTracking()
            .AnyAsync(site => site.Domain == normalizedDomain, cancellationToken);
        if (!siteFound)
        {
            return new WebmasterOffersSearchResult
            {
                Domain = normalizedDomain,
                SiteFound = false,
                Offers = []
            };
        }

        var offers = await LoadFullOffersAsync([normalizedDomain], cancellationToken);

        return new WebmasterOffersSearchResult
        {
            Domain = normalizedDomain,
            SiteFound = true,
            Offers = offers.Select(MapOffer).ToList()
        };
    }

    private Task<List<SiteWebmasterOffer>> LoadFullOffersAsync(
        IReadOnlyList<string> domains,
        CancellationToken cancellationToken)
        => _context.SiteWebmasterOffers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(offer => offer.Webmaster)
            .Include(offer => offer.Prices)
            .Include(offer => offer.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .Where(offer => domains.Contains(offer.SiteDomain))
            .OrderBy(offer => offer.Prices.Any(price => price.WebmasterPriceUsd.HasValue) ? 0 : 1)
            .ThenBy(offer =>
                offer.Prices
                    .Where(price =>
                        price.PriceType == WebmasterOfferPriceType.Main &&
                        price.WebmasterPriceUsd.HasValue)
                    .Min(price => price.WebmasterPriceUsd) ??
                offer.Prices
                    .Where(price => price.WebmasterPriceUsd.HasValue)
                    .Min(price => price.WebmasterPriceUsd))
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Id)
            .ToListAsync(cancellationToken);

    public async Task<WebmasterOffersSummarySearchResult> GetSummaryByDomainAsync(
        string? domain,
        CancellationToken cancellationToken = default)
    {
        var normalizedDomain = DomainNormalizer.Normalize(domain);
        var siteFound = !string.IsNullOrEmpty(normalizedDomain)
            && await _context.Sites.AsNoTracking()
                .AnyAsync(site => site.Domain == normalizedDomain, cancellationToken);
        if (!siteFound)
        {
            return new WebmasterOffersSummarySearchResult { Domain = normalizedDomain ?? string.Empty };
        }

        var offers = await LoadSummaryOffersAsync([normalizedDomain!], cancellationToken);
        return new WebmasterOffersSummarySearchResult
        {
            Domain = normalizedDomain!,
            SiteFound = true,
            Offers = offers.Select(row => row.Offer).ToList()
        };
    }

    private async Task<List<SummaryOfferRow>> LoadSummaryOffersAsync(
        IReadOnlyList<string> domains,
        CancellationToken cancellationToken)
    {
        // Neither webmaster identities nor price rows are loaded or used to order restricted results.
        var offers = await _context.SiteWebmasterOffers
            .AsNoTracking()
            .Where(offer => domains.Contains(offer.SiteDomain))
            .OrderByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Id)
            .Select(offer => new SummaryOfferRow(offer.SiteDomain, new WebmasterOfferSummaryDto
            {
                Id = offer.Id,
                OutreachSenderRawText = offer.OutreachSenderRawText,
                LinkbuilderMailboxRawText = offer.LinkbuilderMailboxRawText,
                LinkPolicyText = offer.LinkPolicyText,
                DfLinksRawText = offer.DfLinksRawText,
                SponsoredTagRawText = offer.SponsoredTagRawText,
                CommentText = offer.CommentText,
                ClientRawText = offer.ClientRawText,
                TermRawText = offer.TermRawText,
                TermType = offer.TermType,
                TermValue = offer.TermValue,
                TermUnit = offer.TermUnit,
                Status = offer.Status,
                CreatedAtUtc = offer.CreatedAtUtc,
                UpdatedAtUtc = offer.UpdatedAtUtc,
                UpdatedBy = offer.UpdatedBy,
                Version = offer.Version,
                LinkbuilderMailboxes = offer.LinkbuilderMailboxes
                    .OrderBy(link => link.LinkbuilderMailbox.DisplayName)
                    .ThenBy(link => link.LinkbuilderMailbox.Email)
                    .Select(link => new WebmasterOfferMailboxDto
                    {
                        Id = link.LinkbuilderMailboxId,
                        Email = link.LinkbuilderMailbox.Email,
                        DisplayName = link.LinkbuilderMailbox.DisplayName,
                        Source = link.Source
                    }).ToList()
            })).ToListAsync(cancellationToken);

        foreach (var offer in offers)
        {
            offer.Offer.TermLabel = FormatTermLabel(offer.Offer.TermType, offer.Offer.TermValue, offer.Offer.TermUnit);
        }

        return offers;
    }

    public async Task<WebmasterOffersMultiSearchResult<WebmasterOfferDto>> MultiSearchAsync(
        string? queryText,
        CancellationToken cancellationToken = default)
    {
        var parsed = MultiSearchParser.Parse(queryText);
        var existingDomains = await GetExistingDomainsAsync(parsed.UniqueDomains, cancellationToken);
        var offers = await LoadFullOffersAsync(existingDomains, cancellationToken);
        return CreateMultiSearchResult(parsed, existingDomains, offers.ToLookup(offer => offer.SiteDomain, MapOffer));
    }

    public async Task<WebmasterOffersMultiSearchResult<WebmasterOfferSummaryDto>> MultiSearchSummaryAsync(
        string? queryText,
        CancellationToken cancellationToken = default)
    {
        var parsed = MultiSearchParser.Parse(queryText);
        var existingDomains = await GetExistingDomainsAsync(parsed.UniqueDomains, cancellationToken);
        var offers = await LoadSummaryOffersAsync(existingDomains, cancellationToken);
        return CreateMultiSearchResult(parsed, existingDomains, offers.ToLookup(row => row.Domain, row => row.Offer));
    }

    private Task<List<string>> GetExistingDomainsAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken)
        => _context.Sites.AsNoTracking()
            .Where(site => domains.Contains(site.Domain))
            .Select(site => site.Domain)
            .ToListAsync(cancellationToken);

    private static WebmasterOffersMultiSearchResult<TOffer> CreateMultiSearchResult<TOffer>(
        MultiSearchParseResult parsed,
        IReadOnlyList<string> existingDomains,
        ILookup<string, TOffer> offers)
    {
        var existing = existingDomains.ToHashSet(StringComparer.Ordinal);
        return new WebmasterOffersMultiSearchResult<TOffer>
        {
            InputCount = parsed.InputCount,
            UniqueDomainCount = parsed.UniqueDomains.Count,
            Duplicates = parsed.Duplicates,
            NotFoundDomains = parsed.UniqueDomains.Where(domain => !existing.Contains(domain)).ToList(),
            DomainsWithoutOffers = parsed.UniqueDomains.Where(domain => existing.Contains(domain) && !offers.Contains(domain)).ToList(),
            Results = parsed.UniqueDomains.Where(offers.Contains)
                .Select(domain => new WebmasterOffersDomainResult<TOffer> { Domain = domain, Offers = offers[domain].ToList() })
                .ToList()
        };
    }

    private sealed record SummaryOfferRow(string Domain, WebmasterOfferSummaryDto Offer);

    public async Task<WebmasterOfferEditDto?> GetForEditAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        var offer = await _context.SiteWebmasterOffers
            .AsNoTracking()
            .Include(item => item.Webmaster)
            .Include(item => item.Prices)
            .Include(item => item.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .FirstOrDefaultAsync(item => item.Id == offerId, cancellationToken);
        if (offer is null)
        {
            return null;
        }

        var selectedMailboxIds = offer.LinkbuilderMailboxes
            .Select(link => link.LinkbuilderMailboxId)
            .ToHashSet();
        var availableMailboxes = await _context.LinkbuilderMailboxes
            .AsNoTracking()
            .Where(mailbox => mailbox.IsActive || selectedMailboxIds.Contains(mailbox.Id))
            .OrderBy(mailbox => mailbox.DisplayName)
            .ThenBy(mailbox => mailbox.Email)
            .Select(mailbox => new WebmasterOfferMailboxOptionDto
            {
                Id = mailbox.Id,
                Email = mailbox.Email,
                DisplayName = mailbox.DisplayName,
                IsActive = mailbox.IsActive
            })
            .ToListAsync(cancellationToken);
        var webmasterOfferCount = await _context.SiteWebmasterOffers
            .AsNoTracking()
            .CountAsync(item => item.WebmasterId == offer.WebmasterId, cancellationToken);

        return new WebmasterOfferEditDto
        {
            Offer = MapOffer(offer),
            WebmasterVersion = offer.Webmaster.Version,
            WebmasterOfferCount = webmasterOfferCount,
            AvailableMailboxes = availableMailboxes
        };
    }

    public async Task<WebmasterOfferUpdateResult> UpdateAsync(
        Guid offerId,
        UpdateWebmasterOfferRequest request,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var offer = await _context.SiteWebmasterOffers
            .Include(item => item.Webmaster)
            .Include(item => item.Prices)
            .Include(item => item.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .FirstOrDefaultAsync(item => item.Id == offerId, cancellationToken);
        if (offer is null)
        {
            return new WebmasterOfferUpdateResult { Status = WebmasterOfferUpdateStatus.NotFound };
        }

        var allowedMailboxIds = offer.LinkbuilderMailboxes
            .Select(link => link.LinkbuilderMailboxId)
            .ToHashSet();
        var requestedMailboxIds = request.LinkbuilderMailboxIds.ToHashSet();
        var requestedMailboxes = await _context.LinkbuilderMailboxes
            .Where(mailbox => requestedMailboxIds.Contains(mailbox.Id))
            .ToListAsync(cancellationToken);
        foreach (var mailbox in requestedMailboxes.Where(mailbox => mailbox.IsActive))
        {
            allowedMailboxIds.Add(mailbox.Id);
        }

        if (!requestedMailboxIds.IsSubsetOf(allowedMailboxIds))
        {
            throw new RequestValidationException("One or more selected linkbuilder mailboxes are unavailable.");
        }

        request.PrimaryEmail = WebmasterPrimaryEmailRepairParser.Normalize(request.PrimaryEmail)
            ?? throw new RequestValidationException("Primary email is required.");
        var historyBefore = EntityChangeHistoryRecorder.CaptureWebmasterOffer(offer);
        var sourceWebmasterId = offer.WebmasterId;
        var now = DateTime.UtcNow;
        var targetResolution = await ResolveTargetWebmasterAsync(offer, request, now, cancellationToken);
        var targetWebmaster = targetResolution.Webmaster;
        var targetWebmasterChanged = !targetResolution.IsNew
            && !string.Equals(targetWebmaster.PrimaryEmail, request.PrimaryEmail, StringComparison.Ordinal);

        if (targetWebmaster.Id != sourceWebmasterId)
        {
            offer.WebmasterId = targetWebmaster.Id;
            offer.Webmaster = targetWebmaster;
        }

        offer.Status = request.Status;
        offer.ContactRawText = request.ContactRawText;
        targetWebmaster.PrimaryEmail = request.PrimaryEmail;
        offer.OutreachSenderRawText = request.OutreachSenderRawText;
        offer.LinkPolicyText = request.LinkPolicyText;
        offer.DfLinksRawText = request.DfLinksRawText;
        offer.SponsoredTagRawText = request.SponsoredTagRawText;
        offer.CommentText = request.CommentText;
        offer.ClientRawText = request.ClientRawText;
        offer.TermType = request.TermType;
        offer.TermValue = request.TermValue;
        offer.TermUnit = request.TermUnit;

        UpdatePrices(offer, request.Prices, now);
        UpdateMailboxes(offer, requestedMailboxIds, requestedMailboxes, now);

        var historyAfter = EntityChangeHistoryRecorder.CaptureWebmasterOffer(offer);
        var hasChanges = EntityChangeHistoryRecorder.RecordUpdate(
            _context,
            EntityChangeHistoryConstants.WebmasterOfferEntityType,
            offer.Id.ToString("D"),
            historyBefore,
            historyAfter,
            EntityChangeHistoryConstants.ManualSource,
            userEmail,
            now);

        if (!hasChanges)
        {
            return new WebmasterOfferUpdateResult
            {
                Status = WebmasterOfferUpdateStatus.Success,
                Offer = MapOffer(offer)
            };
        }

        offer.UpdatedAtUtc = now;
        offer.UpdatedBy = AuditUserFormatter.Format(userEmail);
        if (targetWebmasterChanged)
        {
            targetWebmaster.UpdatedAtUtc = now;
        }

        _context.Entry(offer)
            .Property(item => item.Version)
            .OriginalValue = request.ExpectedVersion;
        if (targetWebmasterChanged && targetWebmaster.Id == sourceWebmasterId)
        {
            _context.Entry(targetWebmaster)
                .Property(item => item.Version)
                .OriginalValue = request.ExpectedWebmasterVersion;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return new WebmasterOfferUpdateResult { Status = WebmasterOfferUpdateStatus.Conflict };
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType"
                    or "PK_SiteWebmasterOfferLinkbuilderMailboxes"
                    or "IX_Webmasters_NormalizedContactRawText"
            })
        {
            // A concurrent child or webmaster insert can hit its unique key before EF checks versions.
            _context.ChangeTracker.Clear();
            return new WebmasterOfferUpdateResult { Status = WebmasterOfferUpdateStatus.Conflict };
        }

        return new WebmasterOfferUpdateResult
        {
            Status = WebmasterOfferUpdateStatus.Success,
            Offer = MapOffer(offer)
        };
    }

    public Task<IReadOnlyList<EntityChangeHistoryDto>> GetHistoryAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
        => EntityChangeHistoryRecorder.GetHistoryAsync(
            _context,
            EntityChangeHistoryConstants.WebmasterOfferEntityType,
            offerId.ToString("D"),
            cancellationToken);

    private void UpdatePrices(
        SiteWebmasterOffer offer,
        IReadOnlyCollection<UpdateWebmasterOfferPriceRequest> requestedPrices,
        DateTime now)
    {
        var existingPrices = offer.Prices.ToDictionary(price => price.PriceType);
        var requestedPriceTypes = requestedPrices.Select(price => price.PriceType).ToHashSet();

        foreach (var existing in offer.Prices.Where(price => !requestedPriceTypes.Contains(price.PriceType)).ToList())
        {
            _context.WebmasterOfferPrices.Remove(existing);
            offer.Prices.Remove(existing);
        }

        foreach (var request in requestedPrices)
        {
            if (!existingPrices.TryGetValue(request.PriceType, out var price))
            {
                price = new WebmasterOfferPrice
                {
                    Id = Guid.NewGuid(),
                    SiteWebmasterOfferId = offer.Id,
                    PriceType = request.PriceType,
                    CreatedAtUtc = now
                };
                offer.Prices.Add(price);
                // With an assigned GUID, navigation discovery would treat this new row as an update.
                _context.WebmasterOfferPrices.Add(price);
            }

            price.AvailabilityStatus = request.AvailabilityStatus;
            price.WebmasterPriceUsd = request.WebmasterPriceUsd;
            price.WebmasterPriceDetails = request.WebmasterPriceDetails;
            price.TermType = offer.TermType;
            price.TermValue = offer.TermValue;
            price.TermUnit = offer.TermUnit;
            price.UpdatedAtUtc = now;
        }
    }

    private void UpdateMailboxes(
        SiteWebmasterOffer offer,
        IReadOnlySet<Guid> requestedMailboxIds,
        IReadOnlyCollection<LinkbuilderMailbox> requestedMailboxes,
        DateTime now)
    {
        foreach (var existing in offer.LinkbuilderMailboxes
                     .Where(link => !requestedMailboxIds.Contains(link.LinkbuilderMailboxId))
                     .ToList())
        {
            _context.SiteWebmasterOfferLinkbuilderMailboxes.Remove(existing);
            offer.LinkbuilderMailboxes.Remove(existing);
        }

        var existingIds = offer.LinkbuilderMailboxes
            .Select(link => link.LinkbuilderMailboxId)
            .ToHashSet();
        foreach (var mailbox in requestedMailboxes.Where(mailbox => !existingIds.Contains(mailbox.Id)))
        {
            offer.LinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
            {
                SiteWebmasterOfferId = offer.Id,
                LinkbuilderMailboxId = mailbox.Id,
                LinkbuilderMailbox = mailbox,
                Source = LinkbuilderMailboxOfferSource.Manual,
                CreatedAtUtc = now
            });
        }
    }

    private static WebmasterOfferDto MapOffer(SiteWebmasterOffer offer)
    {
        return new WebmasterOfferDto
        {
            Id = offer.Id,
            PrimaryEmail = offer.Webmaster.PrimaryEmail,
            ContactRawText = offer.ContactRawText,
            OutreachSenderRawText = offer.OutreachSenderRawText,
            LinkbuilderMailboxRawText = offer.LinkbuilderMailboxRawText,
            LinkPolicyText = offer.LinkPolicyText,
            DfLinksRawText = offer.DfLinksRawText,
            SponsoredTagRawText = offer.SponsoredTagRawText,
            CommentText = offer.CommentText,
            ClientRawText = offer.ClientRawText,
            TermRawText = offer.TermRawText,
            TermType = offer.TermType,
            TermValue = offer.TermValue,
            TermUnit = offer.TermUnit,
            TermLabel = FormatTermLabel(offer.TermType, offer.TermValue, offer.TermUnit),
            Status = offer.Status,
            CreatedAtUtc = offer.CreatedAtUtc,
            UpdatedAtUtc = offer.UpdatedAtUtc,
            UpdatedBy = offer.UpdatedBy,
            Version = offer.Version,
            LinkbuilderMailboxes = offer.LinkbuilderMailboxes
                .OrderBy(link => link.LinkbuilderMailbox.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.LinkbuilderMailbox.Email, StringComparer.OrdinalIgnoreCase)
                .Select(link => new WebmasterOfferMailboxDto
                {
                    Id = link.LinkbuilderMailboxId,
                    Email = link.LinkbuilderMailbox.Email,
                    DisplayName = link.LinkbuilderMailbox.DisplayName,
                    Source = link.Source
                })
                .ToList(),
            Prices = offer.Prices
                .OrderBy(price => price.PriceType)
                .Select(price => new WebmasterOfferPriceDto
                {
                    Id = price.Id,
                    PriceType = price.PriceType,
                    AvailabilityStatus = price.AvailabilityStatus,
                    WebmasterPriceUsd = price.WebmasterPriceUsd,
                    WebmasterPriceDetails = price.WebmasterPriceDetails,
                    TermType = price.TermType,
                    TermValue = price.TermValue,
                    TermUnit = price.TermUnit,
                    TermLabel = FormatTermLabel(price.TermType, price.TermValue, price.TermUnit)
                })
                .ToList()
        };
    }

    private Webmaster CreateWebmaster(
        string contactRawText,
        string normalizedContactRawText,
        string? primaryEmail,
        DateTime now)
    {
        var webmaster = new Webmaster
        {
            Id = Guid.NewGuid(),
            ContactRawText = contactRawText,
            NormalizedContactRawText = normalizedContactRawText,
            PrimaryEmail = primaryEmail,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.Webmasters.Add(webmaster);
        return webmaster;
    }

    private async Task<WebmasterResolution> ResolveTargetWebmasterAsync(
        SiteWebmasterOffer offer,
        UpdateWebmasterOfferRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedContact = WebmasterContactNormalizer.Normalize(request.ContactRawText);
        if (string.Equals(
                normalizedContact,
                offer.Webmaster.NormalizedContactRawText,
                StringComparison.Ordinal))
        {
            return new WebmasterResolution(offer.Webmaster, IsNew: false);
        }

        var existingWebmaster = await _context.Webmasters
            .FirstOrDefaultAsync(
                webmaster => webmaster.NormalizedContactRawText == normalizedContact,
                cancellationToken);
        if (existingWebmaster is not null)
        {
            if (!PrimaryEmailsEquivalent(existingWebmaster.PrimaryEmail, request.PrimaryEmail))
            {
                var existingPrimaryEmail = existingWebmaster.PrimaryEmail ?? "empty";
                throw new RequestValidationException(
                    $"Contact matches an existing webmaster whose Primary email is {existingPrimaryEmail}. " +
                    "Use that Primary email and save again; the existing webmaster will not be overwritten automatically.");
            }

            return new WebmasterResolution(existingWebmaster, IsNew: false);
        }

        return new WebmasterResolution(
            CreateWebmaster(request.ContactRawText, normalizedContact, request.PrimaryEmail, now),
            IsNew: true);
    }

    private static bool PrimaryEmailsEquivalent(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record WebmasterResolution(Webmaster Webmaster, bool IsNew);

    private static string FormatTermLabel(TermType? termType, int? termValue, TermUnit? termUnit)
        => termType switch
        {
            TermType.Permanent when termValue is null && termUnit is null => "Permanent",
            TermType.Finite when termValue is > 0 && termUnit == TermUnit.Year =>
                termValue.Value == 1 ? "1 year" : $"{termValue.Value} years",
            _ => "No term"
        };
}
