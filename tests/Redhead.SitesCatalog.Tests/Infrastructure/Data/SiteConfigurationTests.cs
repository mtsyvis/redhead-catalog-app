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

    [Fact]
    public void SitePriceOptionConfiguration_ConfiguresRequiredTablesConstraintsAndIndexes()
    {
        // Arrange
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        // Act
        var entityType = model.FindEntityType(typeof(SitePriceOption));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("SitePriceOptions", entityType.GetTableName());
        Assert.False(entityType.FindProperty(nameof(SitePriceOption.TermKey))!.IsNullable);

        var uniqueIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(SitePriceOption.SiteDomain),
                nameof(SitePriceOption.PriceType),
                nameof(SitePriceOption.TermKey)
            ]));
        Assert.True(uniqueIndex.IsUnique);

        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(SitePriceOption.PriceType),
                nameof(SitePriceOption.TermKey),
                nameof(SitePriceOption.AmountUsd)
            ]));
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(SitePriceOption.SiteDomain),
                nameof(SitePriceOption.PriceType)
            ]));

        var amountConstraint = Assert.Single(
            entityType.GetCheckConstraints(),
            constraint => constraint.Name == "CK_SitePriceOptions_AmountUsd_Positive");
        Assert.Equal("\"AmountUsd\" > 0", amountConstraint.Sql);
    }

    [Fact]
    public void SiteServiceAvailabilityConfiguration_ConfiguresOptionalServiceTableAndUniqueIndex()
    {
        // Arrange
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        // Act
        var entityType = model.FindEntityType(typeof(SiteServiceAvailability));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("SiteServiceAvailabilities", entityType.GetTableName());

        var uniqueIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(SiteServiceAvailability.SiteDomain),
                nameof(SiteServiceAvailability.ServiceType)
            ]));
        Assert.True(uniqueIndex.IsUnique);

        var notMainConstraint = Assert.Single(
            entityType.GetCheckConstraints(),
            constraint => constraint.Name == "CK_SiteServiceAvailabilities_ServiceType_NotMain");
        Assert.Equal("\"ServiceType\" <> 0", notMainConstraint.Sql);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
