using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Audit;
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

        var offers = await _context.SiteWebmasterOffers
            .AsNoTracking()
            .Include(offer => offer.Webmaster)
            .Include(offer => offer.Prices)
            .Include(offer => offer.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .Where(offer => offer.SiteDomain == normalizedDomain)
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

        return new WebmasterOffersSearchResult
        {
            Domain = normalizedDomain,
            SiteFound = true,
            Offers = offers.Select(MapOffer).ToList()
        };
    }

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

        return new WebmasterOfferEditDto
        {
            Offer = MapOffer(offer),
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

        if (offer.UpdatedAtUtc != request.ExpectedUpdatedAtUtc)
        {
            return new WebmasterOfferUpdateResult { Status = WebmasterOfferUpdateStatus.Conflict };
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

        var historyBefore = EntityChangeHistoryRecorder.CaptureWebmasterOffer(offer);
        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Ticks - (utcNow.Ticks % 10), DateTimeKind.Utc);

        offer.Status = request.Status;
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

        await _context.SaveChangesAsync(cancellationToken);

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

    private static string FormatTermLabel(TermType? termType, int? termValue, TermUnit? termUnit)
        => termType switch
        {
            TermType.Permanent when termValue is null && termUnit is null => "Permanent",
            TermType.Finite when termValue is > 0 && termUnit == TermUnit.Year =>
                termValue.Value == 1 ? "1 year" : $"{termValue.Value} years",
            _ => "No term"
        };
}
