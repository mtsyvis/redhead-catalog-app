namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public interface IGoogleDriveOAuthStateService
{
    string CreateState(string userId);
    bool ValidateAndConsumeState(string userId, string? state);
}
