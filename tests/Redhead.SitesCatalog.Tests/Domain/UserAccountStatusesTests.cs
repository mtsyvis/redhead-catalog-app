using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Tests.Domain;

public class UserAccountStatusesTests
{
    public static TheoryData<bool, DateTime?, DateTime?, string> Cases => new()
    {
        { false, null, null, UserAccountStatuses.Disabled },
        { true, DateTime.UtcNow, null, UserAccountStatuses.Active },
        { true, null, DateTime.UtcNow.AddHours(1), UserAccountStatuses.PendingActivation },
        { true, null, DateTime.UtcNow.AddHours(-1), UserAccountStatuses.InvitationExpired },
        { true, null, null, UserAccountStatuses.InvitationExpired }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Resolve_ReturnsExpectedStatus(
        bool isActive,
        DateTime? activatedAtUtc,
        DateTime? invitationExpiresAtUtc,
        string expected)
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = UserAccountStatuses.Resolve(
            isActive,
            activatedAtUtc,
            invitationExpiresAtUtc,
            now);

        // Assert
        Assert.Equal(expected, result);
    }
}
