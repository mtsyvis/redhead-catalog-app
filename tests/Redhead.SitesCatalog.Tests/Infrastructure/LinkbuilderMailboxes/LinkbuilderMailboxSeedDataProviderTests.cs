using Redhead.SitesCatalog.Infrastructure.LinkbuilderMailboxes;

namespace Redhead.SitesCatalog.Tests.Infrastructure.LinkbuilderMailboxes;

public class LinkbuilderMailboxSeedDataProviderTests
{
    [Fact]
    public void Load_NewLinkbuilderMailboxes_ReturnsCanonicalRecordsWithAliases()
    {
        // Arrange
        var expectedMailboxes = new[]
        {
            new
            {
                Email = "ellieyantsan@gmail.com",
                DisplayName = "Ellie Yantsan",
                Aliases = new[] { "ell", "ellie", "элли", "ellie yantsan", "ellieyantsan" }
            },
            new
            {
                Email = "evelina.brown0407@gmail.com",
                DisplayName = "Evelina Brown",
                Aliases = new[] { "evel", "evelina", "evelina brown", "evelina.brown0407" }
            }
        };

        // Act
        var records = LinkbuilderMailboxSeedDataProvider.Load();

        // Assert
        foreach (var expected in expectedMailboxes)
        {
            var record = Assert.Single(records, item => item.Email == expected.Email);
            Assert.Equal(expected.DisplayName, record.DisplayName);
            Assert.Contains(expected.Email, record.Aliases);

            foreach (var alias in expected.Aliases)
            {
                Assert.Contains(alias, record.Aliases);
            }
        }
    }
}
