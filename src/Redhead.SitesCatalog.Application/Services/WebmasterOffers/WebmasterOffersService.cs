using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
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
            .Include(offer => offer.Prices)
            .Include(offer => offer.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .Where(offer => offer.SiteDomain == normalizedDomain)
            .OrderByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.Id)
            .ToListAsync(cancellationToken);

        return new WebmasterOffersSearchResult
        {
            Domain = normalizedDomain,
            SiteFound = true,
            Offers = offers.Select(MapOffer).ToList()
        };
    }

    private static WebmasterOfferDto MapOffer(SiteWebmasterOffer offer)
    {
        return new WebmasterOfferDto
        {
            Id = offer.Id,
            ContactRawText = offer.ContactRawText,
            OutreachSenderRawText = offer.OutreachSenderRawText,
            LinkbuilderMailboxRawText = offer.LinkbuilderMailboxRawText,
            LinkPolicyText = offer.LinkPolicyText,
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
        => termType == TermType.Finite && termValue is > 0 && termUnit == TermUnit.Year
            ? termValue.Value == 1 ? "1 year" : $"{termValue.Value} years"
            : "No term";
}
