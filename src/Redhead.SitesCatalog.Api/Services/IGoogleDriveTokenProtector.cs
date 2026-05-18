namespace Redhead.SitesCatalog.Api.Services;

public interface IGoogleDriveTokenProtector
{
    string ProtectRefreshToken(string refreshToken);
    string UnprotectRefreshToken(string protectedRefreshToken);
}
