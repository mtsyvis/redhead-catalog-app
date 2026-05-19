using Microsoft.AspNetCore.DataProtection;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.GoogleDrive;

public sealed class GoogleDriveTokenProtectorTests
{
    [Fact]
    public void RefreshTokenProtection_RoundTripsWithoutPlaintextProtectedValue()
    {
        var sut = new GoogleDriveTokenProtector(
            DataProtectionProvider.Create(CreateDataProtectionDirectory()));

        var protectedToken = sut.ProtectRefreshToken("refresh-token-value");
        var unprotectedToken = sut.UnprotectRefreshToken(protectedToken);

        Assert.NotEqual("refresh-token-value", protectedToken);
        Assert.Equal("refresh-token-value", unprotectedToken);
    }

    private static DirectoryInfo CreateDataProtectionDirectory()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "redhead-catalog-tests",
            Guid.NewGuid().ToString()));
}
