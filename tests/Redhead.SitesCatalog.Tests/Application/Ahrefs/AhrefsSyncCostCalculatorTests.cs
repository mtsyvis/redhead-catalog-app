using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

namespace Redhead.SitesCatalog.Tests.Application.Ahrefs;

public sealed class AhrefsSyncCostCalculatorTests
{
    [Fact]
    public void CostPerSite_UsesRequiredFieldCosts()
    {
        // Arrange

        // Act
        var cost = AhrefsSyncCostCalculator.CostPerSite;

        // Assert
        Assert.Equal(12, cost);
    }

    [Fact]
    public void CostPerSite_MatchesProductionBatchAnalysisSelect()
    {
        // Arrange
        var fieldCosts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["index"] = 1,
            ["org_traffic"] = 10,
            ["domain_rating"] = 1
        };

        // Act
        var productionSelectCost = AhrefsApiClient.SelectFields
            .Sum(field => fieldCosts[field]);

        // Assert
        Assert.Equal(
            ["index", "org_traffic", "domain_rating"],
            AhrefsApiClient.SelectFields);
        Assert.Equal(12, productionSelectCost);
        Assert.Equal(AhrefsSyncCostCalculator.CostPerSite, productionSelectCost);
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(4, 50)]
    [InlineData(5, 60)]
    [InlineData(100, 1200)]
    [InlineData(101, 1250)]
    public void EstimateUnits_AppliesMinimumCostPerBatch(int rows, long expected)
    {
        // Arrange

        // Act
        var units = AhrefsSyncCostCalculator.EstimateUnits(rows, 100);

        // Assert
        Assert.Equal(expected, units);
    }

    [Fact]
    public void EstimateUnits_ForCurrentProductionCount_ReturnsExpectedUnits()
    {
        // Arrange

        // Act
        var units = AhrefsSyncCostCalculator.EstimateUnits(66_349, 100);

        // Assert
        Assert.Equal(796_188, units);
    }

    [Theory]
    [InlineData(49, 0)]
    [InlineData(50, 4)]
    [InlineData(120, 10)]
    public void GetAffordableSiteCount_RespectsBatchMinimum(long units, int expected)
    {
        // Arrange

        // Act
        var count = AhrefsSyncCostCalculator.GetAffordableSiteCount(units, 100, 100);

        // Assert
        Assert.Equal(expected, count);
    }
}
