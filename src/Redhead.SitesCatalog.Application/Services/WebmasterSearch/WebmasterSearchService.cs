using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Models.WebmasterSearch;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.WebmasterSearch;

public sealed class WebmasterSearchService : IWebmasterSearchService
{
    public const int MinimumContactLength = 3;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    private readonly ApplicationDbContext _context;

    public WebmasterSearchService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WebmasterSearchResult> SearchAsync(
        string? contact,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedContact = contact?.Trim().ToLowerInvariant() ?? string.Empty;
        var effectivePage = Math.Max(page, 1);
        var effectivePageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        if (normalizedContact.Length < MinimumContactLength)
        {
            return new WebmasterSearchResult
            {
                Page = effectivePage,
                PageSize = effectivePageSize
            };
        }

        var query = _context.Webmasters
            .AsNoTracking()
            .Where(webmaster => webmaster.Offers.Any(offer =>
                offer.ContactRawText.ToLower().Contains(normalizedContact)));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Select(webmaster => new WebmasterSearchItemDto
            {
                WebmasterId = webmaster.Id,
                PrimaryEmail = webmaster.PrimaryEmail,
                RepresentativeContactRawText = webmaster.ContactRawText,
                MatchingContactSnippet = webmaster.Offers
                    .Where(offer => offer.ContactRawText.ToLower().Contains(normalizedContact))
                    .OrderByDescending(offer => offer.UpdatedAtUtc)
                    .ThenBy(offer => offer.Id)
                    .Select(offer => offer.ContactRawText)
                    .First(),
                OfferCount = webmaster.Offers.Count,
                ActiveOfferCount = webmaster.Offers.Count(offer =>
                    offer.Status == SiteWebmasterOfferStatus.Active),
                DomainCount = webmaster.Offers
                    .Select(offer => offer.SiteDomain)
                    .Distinct()
                    .Count(),
                LatestOfferUpdatedAtUtc = webmaster.Offers.Max(offer => offer.UpdatedAtUtc)
            })
            .OrderByDescending(item => item.LatestOfferUpdatedAtUtc)
            .ThenBy(item => item.WebmasterId)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new WebmasterSearchResult
        {
            Items = items,
            Page = effectivePage,
            PageSize = effectivePageSize,
            Total = total
        };
    }

    public async Task<WebmasterWorkspaceDto?> GetWorkspaceAsync(
        Guid webmasterId,
        CancellationToken cancellationToken = default)
    {
        var webmaster = await _context.Webmasters
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Offers)
            .ThenInclude(offer => offer.Site)
            .Include(item => item.Offers)
            .ThenInclude(offer => offer.Prices)
            .Include(item => item.Offers)
            .ThenInclude(offer => offer.LinkbuilderMailboxes)
            .ThenInclude(link => link.LinkbuilderMailbox)
            .SingleOrDefaultAsync(item => item.Id == webmasterId, cancellationToken);
        if (webmaster is null)
        {
            return null;
        }

        var relatedDomains = webmaster.Offers
            .Select(offer => offer.SiteDomain)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var otherWebmasterOfferCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (relatedDomains.Count > 0)
        {
            var counts = await _context.SiteWebmasterOffers
                .AsNoTracking()
                .Where(offer => offer.WebmasterId != webmasterId
                    && relatedDomains.Contains(offer.SiteDomain))
                .GroupBy(offer => offer.SiteDomain)
                .Select(group => new
                {
                    Domain = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            foreach (var count in counts)
            {
                otherWebmasterOfferCounts[count.Domain] = count.Count;
            }
        }

        var domains = webmaster.Offers
            .GroupBy(offer => offer.SiteDomain, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var site = group.Select(offer => offer.Site).FirstOrDefault(item => item is not null);
                return new WebmasterWorkspaceDomainDto
                {
                    Domain = group.Key,
                    SiteFound = site is not null,
                    IsQuarantined = site?.IsQuarantined ?? false,
                    QuarantineReason = site?.QuarantineReason,
                    OtherWebmasterOfferCount = otherWebmasterOfferCounts.GetValueOrDefault(group.Key),
                    Offers = group
                        .OrderByDescending(offer => offer.UpdatedAtUtc)
                        .ThenBy(offer => offer.Id)
                        .Select(offer => MapOffer(offer, webmaster.PrimaryEmail))
                        .ToList()
                };
            })
            .ToList();

        return new WebmasterWorkspaceDto
        {
            Webmaster = new WebmasterWorkspaceSummaryDto
            {
                WebmasterId = webmaster.Id,
                PrimaryEmail = webmaster.PrimaryEmail,
                RepresentativeContactRawText = webmaster.ContactRawText,
                OfferCount = webmaster.Offers.Count,
                ActiveOfferCount = webmaster.Offers.Count(offer =>
                    offer.Status == SiteWebmasterOfferStatus.Active),
                DomainCount = domains.Count
            },
            Domains = domains
        };
    }

    private static WebmasterOfferDto MapOffer(SiteWebmasterOffer offer, string? primaryEmail)
    {
        return new WebmasterOfferDto
        {
            Id = offer.Id,
            PrimaryEmail = primaryEmail,
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
