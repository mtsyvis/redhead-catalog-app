using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Redhead.SitesCatalog.Infrastructure.Data.Migrations;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Data;

public class AddTermAwarePricingMigrationTests
{
    [Fact]
    public void UpOperations_BackfillFlatPricesIntoExpectedPriceTypesAndTerms()
    {
        // Arrange
        var migration = new AddTermAwarePricing();

        // Act
        var sql = GetCombinedBackfillSql(migration);

        // Assert
        Assert.Contains("WHEN s.\"TermType\" = 1 THEN 'permanent'", sql);
        Assert.Contains("THEN 'finite:' || s.\"TermValue\"::text || ':year'", sql);
        Assert.Contains("(0::smallint, current_term.\"TermKey\"", sql);
        Assert.Contains("(1::smallint, current_term.\"TermKey\"", sql);
        Assert.Contains("(2::smallint, current_term.\"TermKey\"", sql);
        Assert.Contains("(5::smallint, current_term.\"TermKey\"", sql);
        Assert.Contains("(3::smallint, 'unknown'", sql);
        Assert.Contains("(4::smallint, 'unknown'", sql);
        Assert.Contains("ON CONFLICT (\"SiteDomain\", \"PriceType\", \"TermKey\") DO NOTHING", sql);
    }

    [Fact]
    public void UpOperations_BackfillNumericServicePricesAsAvailable()
    {
        // Arrange
        var migration = new AddTermAwarePricing();

        // Act
        var sql = GetCombinedBackfillSql(migration);

        // Assert
        Assert.Contains("CASE WHEN s.\"PriceCasino\" IS NOT NULL AND s.\"PriceCasino\" > 0 THEN 1::smallint ELSE s.\"PriceCasinoStatus\" END", sql);
        Assert.Contains("CASE WHEN s.\"PriceCrypto\" IS NOT NULL AND s.\"PriceCrypto\" > 0 THEN 1::smallint ELSE s.\"PriceCryptoStatus\" END", sql);
        Assert.Contains("CASE WHEN s.\"PriceLinkInsert\" IS NOT NULL AND s.\"PriceLinkInsert\" > 0 THEN 1::smallint ELSE s.\"PriceLinkInsertStatus\" END", sql);
        Assert.Contains("CASE WHEN s.\"PriceLinkInsertCasino\" IS NOT NULL AND s.\"PriceLinkInsertCasino\" > 0 THEN 1::smallint ELSE s.\"PriceLinkInsertCasinoStatus\" END", sql);
        Assert.Contains("CASE WHEN s.\"PriceDating\" IS NOT NULL AND s.\"PriceDating\" > 0 THEN 1::smallint ELSE s.\"PriceDatingStatus\" END", sql);
        Assert.Contains("ON CONFLICT (\"SiteDomain\", \"ServiceType\") DO NOTHING", sql);
    }

    private static string GetCombinedBackfillSql(AddTermAwarePricing migration)
        => string.Join(
            "\n",
            migration.UpOperations
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));
}
