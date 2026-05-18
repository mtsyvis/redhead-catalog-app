namespace Redhead.SitesCatalog.Api.Services;

public interface IGoogleDriveOAuthStateService
{
    string CreateState(string userId);
    bool ValidateAndConsumeState(string userId, string? state);
}
