using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.GoogleDrive;

public sealed class GoogleDriveOAuthStateServiceTests
{
    [Fact]
    public void ValidateAndConsumeState_WithCreatedState_ReturnsTrueOnce()
    {
        var sut = CreateService();
        var state = sut.CreateState("user-1");

        Assert.True(sut.ValidateAndConsumeState("user-1", state));
        Assert.False(sut.ValidateAndConsumeState("user-1", state));
    }

    [Fact]
    public void ValidateAndConsumeState_WithDifferentUser_ReturnsFalse()
    {
        var sut = CreateService();
        var state = sut.CreateState("user-1");

        Assert.False(sut.ValidateAndConsumeState("user-2", state));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-protected-state")]
    public void ValidateAndConsumeState_WithInvalidState_ReturnsFalse(string? state)
    {
        var sut = CreateService();

        Assert.False(sut.ValidateAndConsumeState("user-1", state));
    }

    private static GoogleDriveOAuthStateService CreateService()
        => new(
            DataProtectionProvider.Create(CreateDataProtectionDirectory()),
            new MemoryCache(new MemoryCacheOptions()));

    private static DirectoryInfo CreateDataProtectionDirectory()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "redhead-catalog-tests",
            Guid.NewGuid().ToString()));
}
