using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class ExportedDomainAccessCleanupService : IExportedDomainAccessCleanupService
{
    private readonly ApplicationDbContext _context;

    public ExportedDomainAccessCleanupService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> DeleteOldAccessesAsync(
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var totalDeleted = 0;
        while (true)
        {
            var oldAccesses = await _context.ExportedDomainAccesses
                .Where(access => access.ExportedAtUtc < cutoffUtc)
                .OrderBy(access => access.ExportedAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (oldAccesses.Count == 0)
            {
                return totalDeleted;
            }

            _context.ExportedDomainAccesses.RemoveRange(oldAccesses);
            await _context.SaveChangesAsync(cancellationToken);

            totalDeleted += oldAccesses.Count;
        }
    }
}
