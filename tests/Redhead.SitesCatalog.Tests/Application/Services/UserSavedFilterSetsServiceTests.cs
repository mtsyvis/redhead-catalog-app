using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.SavedFilters;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class UserSavedFilterSetsServiceTests
{
    [Fact]
    public async Task FilterSetCrud_IsScopedToCurrentUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var settings = CreateSettings(stopListDomains: new[] { "https://www.blocked.com/path" });

        // Act
        var created = await sut.CreateFilterSetAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "  Outreach base  ",
            settings,
            CancellationToken.None);
        await sut.CreateFilterSetAsync(
            "user-2",
            TableViewConstants.SitesTableKey,
            "Other user set",
            settings,
            CancellationToken.None);
        var updated = await sut.UpdateFilterSetAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            created.Id,
            "Renamed",
            CreateSettings(niches: new[] { "Business" }),
            CancellationToken.None);
        var listed = await sut.GetFilterSetsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);
        await sut.DeleteFilterSetAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            created.Id,
            CancellationToken.None);
        var afterDelete = await sut.GetFilterSetsAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            CancellationToken.None);

        // Assert
        Assert.Equal("Outreach base", created.Name);
        Assert.Equal(new[] { "blocked.com" }, created.Settings.StopListDomains);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(new[] { "Business" }, updated.Settings.Niches);
        var onlyCurrentUserSet = Assert.Single(listed.FilterSets);
        Assert.Equal(created.Id, onlyCurrentUserSet.Id);
        Assert.Empty(afterDelete.FilterSets);
        Assert.Equal(1, await db.UserSavedFilterSets.CountAsync(filterSet => filterSet.UserId == "user-2"));
    }

    [Fact]
    public async Task CreateFilterSet_WhenLimitReached_RejectsFilterSet()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        for (var index = 0; index < SavedFilterSetConstants.FilterSetsPerUserTableLimit; index++)
        {
            await sut.CreateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                $"Filter set {index}",
                CreateSettings(),
                CancellationToken.None);
        }

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "One too many",
                CreateSettings(),
                CancellationToken.None));

        // Assert
        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFilterSet_WithDuplicateNameCaseInsensitive_RejectsFilterSet()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await sut.CreateFilterSetAsync(
            "user-1",
            TableViewConstants.SitesTableKey,
            "Casino geo",
            CreateSettings(),
            CancellationToken.None);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                " casino geo ",
                CreateSettings(),
                CancellationToken.None));

        // Assert
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateFilterSet_ForAnotherUser_RejectsFilterSet()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var otherUserSet = await sut.CreateFilterSetAsync(
            "user-2",
            TableViewConstants.SitesTableKey,
            "Other",
            CreateSettings(),
            CancellationToken.None);

        // Act
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.UpdateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                otherUserSet.Id,
                "Rename attempt",
                null,
                CancellationToken.None));

        // Assert
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidTableKey_IsRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.GetFilterSetsAsync("user-1", "users", CancellationToken.None));

        // Assert
        Assert.Contains("Unsupported table key", ex.Message);
    }

    [Fact]
    public async Task InvalidSettings_AreRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var settings = CreateSettings(topicFitMode: "loose");

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "Broken",
                settings,
                CancellationToken.None));

        // Assert
        Assert.Contains("Topic fit mode", ex.Message);
    }

    [Fact]
    public async Task OversizedStopList_IsRejected()
    {
        // Arrange
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        var domains = Enumerable.Range(0, 50001)
            .Select(index => $"example-{index}.com")
            .ToArray();

        // Act
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            sut.CreateFilterSetAsync(
                "user-1",
                TableViewConstants.SitesTableKey,
                "Huge",
                CreateSettings(stopListDomains: domains),
                CancellationToken.None));

        // Assert
        Assert.Contains("at most 50000", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static UserSavedFilterSetsService CreateService(ApplicationDbContext db)
        => new(db);

    private static SavedFilterSettingsDto CreateSettings(
        IReadOnlyCollection<string>? stopListDomains = null,
        IReadOnlyCollection<string>? niches = null,
        string topicFitMode = TopicFitModeValues.Expand,
        string? termKey = null)
        => new()
        {
            SchemaVersion = SavedFilterSetConstants.SchemaVersion,
            StopListDomains = stopListDomains?.ToList(),
            DrMin = "20",
            DrMax = "70",
            TrafficMin = string.Empty,
            TrafficMax = string.Empty,
            PriceMin = string.Empty,
            PriceMax = "500",
            TermKey = termKey,
            LocationSelections = new List<SavedFilterLocationSelectionDto>(),
            ExcludedLocationKeys = new List<string>(),
            Niches = niches?.ToList() ?? new List<string>(),
            CategorySearchTerms = new List<string> { "sports betting" },
            TopicFitMode = topicFitMode,
            ExcludedNiches = new List<string>(),
            ExcludedCategorySearchTerms = new List<string>(),
            Languages = new List<string>(),
            CasinoAvailability = new List<string>(),
            CryptoAvailability = new List<string>(),
            LinkInsertAvailability = new List<string>(),
            LinkInsertCasinoAvailability = new List<string>(),
            DatingAvailability = new List<string>(),
            Quarantine = QuarantineFilterValues.Exclude,
            LastPublishedFromMonth = null,
            LastPublishedToMonth = null
        };
}
