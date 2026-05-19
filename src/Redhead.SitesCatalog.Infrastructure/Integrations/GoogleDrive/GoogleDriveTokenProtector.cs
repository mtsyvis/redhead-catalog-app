using Microsoft.AspNetCore.DataProtection;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public sealed class GoogleDriveTokenProtector : IGoogleDriveTokenProtector
{
    private const string Purpose = "Redhead.SitesCatalog.GoogleDrive.RefreshToken.v1";
    private readonly IDataProtector _protector;

    public GoogleDriveTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string ProtectRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        return _protector.Protect(refreshToken);
    }

    public string UnprotectRefreshToken(string protectedRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(protectedRefreshToken))
        {
            throw new ArgumentException("Protected refresh token is required.", nameof(protectedRefreshToken));
        }

        return _protector.Unprotect(protectedRefreshToken);
    }
}
