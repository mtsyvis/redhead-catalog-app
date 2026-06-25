using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class LiteMultiSearchUsageService : ILiteMultiSearchUsageService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public LiteMultiSearchUsageService(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<LiteMultiSearchUsageResult> TryConsumeAsync(
        string userId,
        int uniqueDomainCount,
        CancellationToken cancellationToken = default)
    {
        if (uniqueDomainCount == 0)
        {
            return new LiteMultiSearchUsageResult(
                LiteMultiSearchUsageStatus.Allowed,
                DomainsRequested: 0,
                DomainsUsed: 0,
                LiteMultiSearchConstants.MonthlyDomainLimit,
                RemainingAfterRequest: LiteMultiSearchConstants.MonthlyDomainLimit);
        }

        if (uniqueDomainCount > LiteMultiSearchConstants.MaxDomainsPerRequest)
        {
            return new LiteMultiSearchUsageResult(
                LiteMultiSearchUsageStatus.RequestLimitExceeded,
                uniqueDomainCount,
                DomainsUsed: 0,
                LiteMultiSearchConstants.MonthlyDomainLimit,
                RemainingAfterRequest: LiteMultiSearchConstants.MonthlyDomainLimit);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usage = await _context.LiteMultiSearchUsages
            .SingleOrDefaultAsync(
                item => item.UserId == userId && item.MonthStartUtc == monthStart,
                cancellationToken);
        var domainsUsed = usage?.DomainsUsed ?? 0;

        if (domainsUsed + uniqueDomainCount > LiteMultiSearchConstants.MonthlyDomainLimit)
        {
            return new LiteMultiSearchUsageResult(
                LiteMultiSearchUsageStatus.MonthlyLimitExceeded,
                uniqueDomainCount,
                domainsUsed,
                LiteMultiSearchConstants.MonthlyDomainLimit,
                Math.Max(0, LiteMultiSearchConstants.MonthlyDomainLimit - domainsUsed));
        }

        if (usage == null)
        {
            usage = new LiteMultiSearchUsage
            {
                UserId = userId,
                MonthStartUtc = monthStart,
                DomainsUsed = uniqueDomainCount,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _context.LiteMultiSearchUsages.Add(usage);
        }
        else
        {
            usage.DomainsUsed += uniqueDomainCount;
            usage.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new LiteMultiSearchUsageResult(
            LiteMultiSearchUsageStatus.Allowed,
            uniqueDomainCount,
            usage.DomainsUsed,
            LiteMultiSearchConstants.MonthlyDomainLimit,
            LiteMultiSearchConstants.MonthlyDomainLimit - usage.DomainsUsed);
    }
}
