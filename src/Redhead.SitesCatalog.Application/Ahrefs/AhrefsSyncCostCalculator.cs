namespace Redhead.SitesCatalog.Application.Ahrefs;

public static class AhrefsSyncCostCalculator
{
    public const int IndexCost = 1;
    public const int OrganicTrafficCost = 10;
    public const int DomainRatingCost = 1;
    public const int CostPerSite = IndexCost + OrganicTrafficCost + DomainRatingCost;
    public const int MinimumBatchCost = 50;

    public static long EstimateUnits(int rowCount, int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        long units = 0;
        for (var remaining = rowCount; remaining > 0; remaining -= batchSize)
        {
            var batchRows = Math.Min(batchSize, remaining);
            units += Math.Max(MinimumBatchCost, CostPerSite * batchRows);
        }

        return units;
    }

    public static int GetAffordableSiteCount(long spendableUnits, int eligibleCount, int batchSize)
    {
        if (spendableUnits <= 0 || eligibleCount <= 0)
        {
            return 0;
        }

        var candidate = (int)Math.Min(eligibleCount, spendableUnits / CostPerSite);
        while (candidate > 0 && EstimateUnits(candidate, batchSize) > spendableUnits)
        {
            candidate--;
        }

        return candidate;
    }
}
