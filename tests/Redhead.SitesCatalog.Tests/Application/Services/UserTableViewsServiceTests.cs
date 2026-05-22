using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.TableViews;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class UserTableViewsServiceTests
{
    [Fact]
    public async Task CustomViewCrud_IsScopedToCurrentUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var settings = CreateSettings("domain", "dr");

        // Act
        var created = await sut.CreateCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "  My view  ",
            settings,
            CancellationToken.None);
        await sut.CreateCustomViewAsync(
            "user-2",
            TableViewConstants.SitesTableKey,
            "Other user view",
            settings,
            CancellationToken.None);
        var updated = await sut.UpdateCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            created.Id,
            "Renamed",
            CreateSettings("domain", "traffic"),
            CancellationToken.None);
        var listed = await sut.GetTableViewsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);
        await sut.DeleteCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            created.Id,
            CancellationToken.None);
        var afterDelete = await sut.GetTableViewsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);

        // Assert
        Assert.Equal("My view", created.Name);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(new[] { "domain", "traffic" }, updated.Settings.VisibleColumnIds);
        var onlyCurrentUserView = Assert.Single(listed.CustomViews);
        Assert.Equal(created.Id, onlyCurrentUserView.Id);
        Assert.Empty(afterDelete.CustomViews);
        Assert.Equal(1, await db.UserTableCustomViews.CountAsync(view => view.UserId == "user-2"));
    }

    [Fact]
    public async Task CreateCustomView_WhenLimitReached_RejectsView()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        for (var index = 0; index < TableViewConstants.CustomViewsPerUserTableLimit; index++)
        {
            await sut.CreateCustomViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                $"View {index}",
                CreateSettings("domain"),
                CancellationToken.None);
        }

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateCustomViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "One too many",
                CreateSettings("domain"),
                CancellationToken.None));

        // Assert
        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCustomView_WithDuplicateNameCaseInsensitive_RejectsView()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await sut.CreateCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "Pricing",
            CreateSettings("domain"),
            CancellationToken.None);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateCustomViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                " pricing ",
                CreateSettings("domain"),
                CancellationToken.None));

        // Assert
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetActiveView_ToSystemView_PersistsPreference()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        // Act
        await sut.SetActiveViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "system",
            "pricing",
            CancellationToken.None);
        var response = await sut.GetTableViewsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);

        // Assert
        Assert.Equal(TableViewConstants.SystemViewType, response.ActiveViewType);
        Assert.Equal("pricing", response.ActiveViewKey);
    }

    [Fact]
    public async Task SetActiveView_ToOwnedCustomView_PersistsPreference()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var view = await sut.CreateCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "Owned",
            CreateSettings("domain"),
            CancellationToken.None);

        // Act
        await sut.SetActiveViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "custom",
            view.Id.ToString(),
            CancellationToken.None);
        var response = await sut.GetTableViewsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);

        // Assert
        Assert.Equal(TableViewConstants.CustomViewType, response.ActiveViewType);
        Assert.Equal(view.Id.ToString(), response.ActiveViewKey);
    }

    [Fact]
    public async Task SetActiveView_ToAnotherUsersCustomView_RejectsView()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var otherUserView = await sut.CreateCustomViewAsync(
            "user-2",
            TableViewConstants.SitesTableKey,
            "Other",
            CreateSettings("domain"),
            CancellationToken.None);

        // Act
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.SetActiveViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "custom",
                otherUserView.Id.ToString(),
                CancellationToken.None));

        // Assert
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCustomView_WhenActive_FallsBackToDefaultSystemView()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var view = await sut.CreateCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "Owned",
            CreateSettings("domain"),
            CancellationToken.None);
        await sut.SetActiveViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "custom",
            view.Id.ToString(),
            CancellationToken.None);

        // Act
        await sut.DeleteCustomViewAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            view.Id,
            CancellationToken.None);
        var response = await sut.GetTableViewsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);

        // Assert
        Assert.Equal(TableViewConstants.SystemViewType, response.ActiveViewType);
        Assert.Equal(TableViewConstants.DefaultSystemViewKey, response.ActiveViewKey);
    }

    [Fact]
    public async Task InvalidTableKey_IsRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.GetTableViewsAsync("user-1", "users", CancellationToken.None));

        // Assert
        Assert.Contains("Unsupported table key", ex.Message);
    }

    [Fact]
    public async Task InvalidSettings_AreRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateCustomViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "Broken",
                new TableViewSettingsDto
                {
                    SchemaVersion = TableViewConstants.SchemaVersion,
                    VisibleColumnIds = new List<string> { "domain" },
                    Density = "tiny"
                },
                CancellationToken.None));

        // Assert
        Assert.Contains("Density", ex.Message);
    }

    [Fact]
    public async Task OversizedSettings_AreRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var widths = Enumerable.Range(0, 250)
            .ToDictionary(
                index => $"column_{index:D3}_{new string('x', 85)}",
                _ => 120);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateCustomViewAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "Huge",
                new TableViewSettingsDto
                {
                    SchemaVersion = TableViewConstants.SchemaVersion,
                    VisibleColumnIds = widths.Keys.ToList(),
                    Density = "standard",
                    ColumnWidths = widths
                },
                CancellationToken.None));

        // Assert
        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static UserTableViewsService CreateService(ApplicationDbContext db)
        => new(db);

    private static TableViewSettingsDto CreateSettings(params string[] visibleColumnIds)
        => new()
        {
            SchemaVersion = TableViewConstants.SchemaVersion,
            VisibleColumnIds = visibleColumnIds.ToList(),
            Density = "standard",
            ColumnWidths = new Dictionary<string, int>
            {
                ["domain"] = 240
            }
        };
}
