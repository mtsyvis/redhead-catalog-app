namespace Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

public interface IGoogleDriveIntegrationService
{
    Task<GoogleDriveStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken);
    string CreateAuthorizationUrl(string userId);
    Task CompleteConnectionAsync(string userId, string? code, string? state, CancellationToken cancellationToken);
    Task DisconnectAsync(string userId, CancellationToken cancellationToken);
}
