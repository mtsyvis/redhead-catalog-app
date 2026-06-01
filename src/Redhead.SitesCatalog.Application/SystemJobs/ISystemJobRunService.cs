using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.SystemJobs;

public interface ISystemJobRunService
{
    Task<bool> HasSuccessfulRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default);

    Task<SystemJobRun> StartRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<SystemJobArtifact> AddArtifactAsync(
        Guid runId,
        SystemJobArtifactInput artifact,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default);
}
