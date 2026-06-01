using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Repositories;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Infrastructure.Repositories;

public sealed class SystemJobRunRepository : ISystemJobRunRepository
{
    private readonly ApplicationDbContext _context;

    public SystemJobRunRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasSucceededRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default)
        => _context.SystemJobRuns
            .AsNoTracking()
            .AnyAsync(
                run => run.JobName == jobName
                    && run.PeriodKey == periodKey
                    && run.Status == SystemJobRunStatus.Succeeded,
                cancellationToken);

    public Task<SystemJobRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _context.SystemJobRuns
            .FirstOrDefaultAsync(run => run.Id == id, cancellationToken);

    public async Task AddAsync(
        SystemJobRun run,
        CancellationToken cancellationToken = default)
        => await _context.SystemJobRuns.AddAsync(run, cancellationToken);

    public async Task AddArtifactAsync(
        SystemJobArtifact artifact,
        CancellationToken cancellationToken = default)
        => await _context.SystemJobArtifacts.AddAsync(artifact, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
