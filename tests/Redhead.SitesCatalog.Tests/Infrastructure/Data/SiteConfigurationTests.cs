using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Data;

public class SiteConfigurationTests
{
    [Theory]
    [InlineData("CK_Sites_PriceCasino_StatusConsistency", "PriceCasinoStatus", "PriceCasino")]
    [InlineData("CK_Sites_PriceCrypto_StatusConsistency", "PriceCryptoStatus", "PriceCrypto")]
    [InlineData("CK_Sites_PriceLinkInsert_StatusConsistency", "PriceLinkInsertStatus", "PriceLinkInsert")]
    [InlineData("CK_Sites_PriceLinkInsertCasino_StatusConsistency", "PriceLinkInsertCasinoStatus", "PriceLinkInsertCasino")]
    [InlineData("CK_Sites_PriceDating_StatusConsistency", "PriceDatingStatus", "PriceDating")]
    public void OptionalServiceStatusConstraints_AllowOnlyConsistentStatusAndPricePairs(
        string constraintName,
        string statusColumn,
        string priceColumn)
    {
        // Arrange
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(Site));

        // Act
        var constraint = Assert.Single(
            entityType!.GetCheckConstraints(),
            c => c.Name == constraintName);

        // Assert
        Assert.Equal(
            $"(\"{statusColumn}\" = 1 AND \"{priceColumn}\" IS NOT NULL AND \"{priceColumn}\" > 0) OR (\"{statusColumn}\" IN (0, 2, 3) AND \"{priceColumn}\" IS NULL)",
            constraint.Sql);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
