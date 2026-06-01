using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Repositories;

namespace Redhead.SitesCatalog.Application.SystemJobs;

public sealed class SystemJobRunService : ISystemJobRunService
{
    private readonly ISystemJobRunRepository _repository;

    public SystemJobRunService(ISystemJobRunRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> HasSuccessfulRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default)
        => _repository.HasSucceededRunAsync(jobName, periodKey, cancellationToken);

    public async Task<SystemJobRun> StartRunAsync(
        string jobName,
        string periodKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(periodKey);

        var now = DateTime.UtcNow;
        var run = new SystemJobRun
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            PeriodKey = periodKey,
            Status = SystemJobRunStatus.Running,
            StartedAtUtc = now,
            CreatedAtUtc = now
        };

        await _repository.AddAsync(run, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return run;
    }

    public async Task MarkSucceededAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRequiredRunAsync(runId, cancellationToken);
        var now = DateTime.UtcNow;

        run.Status = SystemJobRunStatus.Succeeded;
        run.FinishedAtUtc = now;
        run.ErrorMessage = null;
        run.UpdatedAtUtc = now;

        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<SystemJobArtifact> AddArtifactAsync(
        Guid runId,
        SystemJobArtifactInput artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifact.FileName);
        if (artifact.FileSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(artifact.FileSizeBytes), "File size cannot be negative.");
        }

        await GetRequiredRunAsync(runId, cancellationToken);

        var entity = new SystemJobArtifact
        {
            Id = Guid.NewGuid(),
            SystemJobRunId = runId,
            FileName = artifact.FileName,
            FileSizeBytes = artifact.FileSizeBytes,
            StorageProvider = artifact.StorageProvider,
            StoragePath = artifact.StoragePath,
            ExternalFileId = artifact.ExternalFileId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddArtifactAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRequiredRunAsync(runId, cancellationToken);
        var now = DateTime.UtcNow;

        run.Status = SystemJobRunStatus.Failed;
        run.FinishedAtUtc = now;
        run.ErrorMessage = errorMessage;
        run.UpdatedAtUtc = now;

        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<SystemJobRun> GetRequiredRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(runId, cancellationToken);
        return run ?? throw new InvalidOperationException($"System job run was not found: {runId}");
    }
}
