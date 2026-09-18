using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Api.Validation;

namespace Redhead.SitesCatalog.Tests.Api.Validation;

public sealed class AnalyticsRequestMapperTests
{
    [Theory]
    [InlineData(1, 10, true)]
    [InlineData(1, 25, true)]
    [InlineData(1, 50, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 25, false)]
    [InlineData(-1, 25, false)]
    [InlineData(int.MinValue, 25, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, -1, false)]
    [InlineData(1, 999, false)]
    [InlineData(int.MaxValue, 25, false)]
    [InlineData(214748365, 10, true)]
    [InlineData(214748366, 10, false)]
    [InlineData(21474837, 100, true)]
    [InlineData(21474838, 100, false)]
    public void PaginatedReports_ApplyTheSameValidation(int page, int pageSize, bool valid)
    {
        // Arrange
        var now = new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        var exportRequest = new ExportActivityAnalyticsRequest { Page = page, PageSize = pageSize };
        var missingRequest = new MissingDomainsAnalyticsRequest { Page = page, PageSize = pageSize };

        // Act
        var export = AnalyticsRequestMapper.ToExportActivityQuery(exportRequest, now);
        var missing = AnalyticsRequestMapper.ToMissingDomainsQuery(missingRequest, now);

        // Assert
        Assert.Equal(valid, export.Error == null);
        Assert.Equal(valid, missing.Error == null);
        Assert.Equal(export.Error, missing.Error);
        if (valid)
        {
            Assert.Equal(page, export.Query!.RecentExportsPage);
            Assert.Equal(pageSize, export.Query.RecentExportsPageSize);
            Assert.Equal(page, missing.Query!.Page);
            Assert.Equal(pageSize, missing.Query.PageSize);
        }
        else
        {
            Assert.Null(export.Query);
            Assert.Null(missing.Query);
        }
    }
}
