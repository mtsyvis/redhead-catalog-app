using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Domain;

public class UserAccountStatusesTests
{
    public static TheoryData<bool, DateTime?, string?, DateTime?, string> Cases => new()
    {
        { false, null, null, null, UserAccountStatuses.Disabled },
        { false, DateTime.UtcNow, "token-hash", DateTime.UtcNow.AddHours(1), UserAccountStatuses.PendingReactivation },
        { false, DateTime.UtcNow, "token-hash", DateTime.UtcNow.AddHours(-1), UserAccountStatuses.ReactivationExpired },
        { true, DateTime.UtcNow, null, null, UserAccountStatuses.Active },
        { true, null, "token-hash", DateTime.UtcNow.AddHours(1), UserAccountStatuses.PendingActivation },
        { true, null, "token-hash", DateTime.UtcNow.AddHours(-1), UserAccountStatuses.InvitationExpired },
        { true, null, null, null, UserAccountStatuses.InvitationExpired }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Resolve_ReturnsExpectedStatus(
        bool isActive,
        DateTime? activatedAtUtc,
        string? invitationTokenHash,
        DateTime? invitationExpiresAtUtc,
        string expected)
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = UserAccountStatuses.Resolve(
            isActive,
            activatedAtUtc,
            invitationTokenHash,
            invitationExpiresAtUtc,
            now);

        // Assert
        Assert.Equal(expected, result);
    }
}
