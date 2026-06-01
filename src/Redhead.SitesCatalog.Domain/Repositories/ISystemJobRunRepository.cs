using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Domain.Repositories;

public interface ISystemJobRunRepository
{
    Task<bool> HasSucceededRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default);

    Task<SystemJobRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SystemJobRun run,
        CancellationToken cancellationToken = default);

    Task AddArtifactAsync(
        SystemJobArtifact artifact,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
