using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Exports;

public sealed class EmergencySitesExportServiceTests
{
    [Fact]
    public async Task GenerateAsync_ExportsAllSitesWithoutUserExportLimits()
    {
        // Arrange
        await using var db = CreateDbContext();
        db.Sites.AddRange(
            new Site
            {
                Domain = "second.example",
                DR = 20,
                Traffic = 200,
                Location = "US",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Site
            {
                Domain = "first.example",
                DR = 10,
                Traffic = 100,
                Location = "US",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new EmergencySitesExportService(db, new SitesExcelExportGenerator());

        // Act
        var result = await sut.GenerateAsync(CancellationToken.None);

        // Assert
        Assert.StartsWith("redhead-sites-full-", result.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", result.FileName, StringComparison.Ordinal);
        Assert.Equal(ExportConstants.ExcelContentType, result.ContentType);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(result.FileStream.Length, result.FileSizeBytes);
        Assert.True(result.FileSizeBytes > 0);

        var rows = XlsxTestWorkbook.ReadRows(result.FileStream, "Sites");
        Assert.Equal(2, rows.Count);
        Assert.Equal("first.example", rows[0]["Domain"]);
        Assert.Equal("second.example", rows[1]["Domain"]);
        Assert.Empty(db.ExportLogs);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
