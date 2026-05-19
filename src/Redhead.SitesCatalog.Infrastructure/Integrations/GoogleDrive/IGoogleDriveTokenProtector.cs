namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public interface IGoogleDriveTokenProtector
{
    string ProtectRefreshToken(string refreshToken);
    string UnprotectRefreshToken(string protectedRefreshToken);
}
