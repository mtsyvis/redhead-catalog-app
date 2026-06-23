using Redhead.SitesCatalog.Api.Security;

namespace Redhead.SitesCatalog.Tests.Api.Security;

public class UserInvitationTokenTests
{
    [Fact]
    public void Generate_CreatesUniqueUrlSafeTokens()
    {
        // Arrange

        // Act
        var first = UserInvitationToken.Generate();
        var second = UserInvitationToken.Generate();

        // Assert
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("+", first);
        Assert.DoesNotContain("/", first);
        Assert.DoesNotContain("=", first);
    }

    [Fact]
    public void Hash_ReturnsStableSha256HexWithoutExposingToken()
    {
        // Arrange
        const string token = "invitation-token";

        // Act
        var first = UserInvitationToken.Hash(token);
        var second = UserInvitationToken.Hash(token);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(token, first, StringComparison.OrdinalIgnoreCase);
    }
}
