using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.SavedFilters;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class UserSavedFilterSetsService : IUserSavedFilterSetsService
{
    private const int MaxScalarLength = 500;
    private const int MaxListItemLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;

    public UserSavedFilterSetsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SavedFilterSetsResponseDto> GetFilterSetsAsync(
        string userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var filterSets = await _db.UserSavedFilterSets
            .AsNoTracking()
            .Where(filterSet => filterSet.UserId == userId && filterSet.TableKey == tableKey)
            .OrderBy(filterSet => filterSet.Name)
            .ToListAsync(cancellationToken);

        return new SavedFilterSetsResponseDto(filterSets.Select(ToDto).ToList());
    }

    public async Task<SavedFilterSetDto> CreateFilterSetAsync(
        string userId,
        string tableKey,
        string name,
        SavedFilterSettingsDto settings,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        if (settings == null)
        {
            throw new RequestValidationException("Saved filter settings are required.");
        }

        var count = await _db.UserSavedFilterSets.CountAsync(
            filterSet => filterSet.UserId == userId && filterSet.TableKey == tableKey,
            cancellationToken);
        if (count >= SavedFilterSetConstants.FilterSetsPerUserTableLimit)
        {
            throw new RequestValidationException(
                $"A maximum of {SavedFilterSetConstants.FilterSetsPerUserTableLimit} saved filter sets is allowed for this table.");
        }

        var normalizedName = await ValidateNameAsync(
            userId,
            tableKey,
            name,
            excludeId: null,
            cancellationToken);
        var settingsJson = ValidateAndSerializeSettings(settings);
        var now = DateTime.UtcNow;

        var filterSet = new UserSavedFilterSet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TableKey = tableKey,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            SchemaVersion = SavedFilterSetConstants.SchemaVersion,
            SettingsJson = settingsJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.UserSavedFilterSets.Add(filterSet);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(filterSet);
    }

    public async Task<SavedFilterSetDto> UpdateFilterSetAsync(
        string userId,
        string tableKey,
        Guid id,
        string? name,
        SavedFilterSettingsDto? settings,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var filterSet = await _db.UserSavedFilterSets.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);
        if (filterSet == null)
        {
            throw new KeyNotFoundException("Saved filter set was not found.");
        }

        if (name != null)
        {
            filterSet.NormalizedName = await ValidateNameAsync(
                userId,
                tableKey,
                name,
                id,
                cancellationToken);
            filterSet.Name = name.Trim();
        }

        if (settings != null)
        {
            filterSet.SettingsJson = ValidateAndSerializeSettings(settings);
            filterSet.SchemaVersion = SavedFilterSetConstants.SchemaVersion;
        }

        filterSet.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(filterSet);
    }

    public async Task DeleteFilterSetAsync(
        string userId,
        string tableKey,
        Guid id,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var filterSet = await _db.UserSavedFilterSets.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);
        if (filterSet == null)
        {
            throw new KeyNotFoundException("Saved filter set was not found.");
        }

        _db.UserSavedFilterSets.Remove(filterSet);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateTableKey(string tableKey)
    {
        if (!string.Equals(tableKey, TableViewConstants.SitesTableKey, StringComparison.Ordinal))
        {
            throw new RequestValidationException("Unsupported table key.");
        }
    }

    private async Task<string> ValidateNameAsync(
        string userId,
        string tableKey,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new RequestValidationException("Saved filter set name is required.");
        }

        if (trimmed.Length > SavedFilterSetConstants.FilterSetNameMaxLength)
        {
            throw new RequestValidationException(
                $"Saved filter set name must be {SavedFilterSetConstants.FilterSetNameMaxLength} characters or fewer.");
        }

        var normalizedName = trimmed.ToUpperInvariant();
        var duplicate = await _db.UserSavedFilterSets.AnyAsync(
            filterSet =>
                filterSet.UserId == userId &&
                filterSet.TableKey == tableKey &&
                filterSet.NormalizedName == normalizedName &&
                (!excludeId.HasValue || filterSet.Id != excludeId.Value),
            cancellationToken);

        if (duplicate)
        {
            throw new RequestValidationException("A saved filter set with this name already exists.");
        }

        return normalizedName;
    }

    private static string ValidateAndSerializeSettings(SavedFilterSettingsDto settings)
    {
        if (settings.SchemaVersion != SavedFilterSetConstants.SchemaVersion)
        {
            throw new RequestValidationException("Unsupported saved filter settings schema version.");
        }

        ValidateScalar(settings.DrMin, "DR minimum");
        ValidateScalar(settings.DrMax, "DR maximum");
        ValidateScalar(settings.TrafficMin, "Traffic minimum");
        ValidateScalar(settings.TrafficMax, "Traffic maximum");
        ValidateScalar(settings.PriceMin, "Price minimum");
        ValidateScalar(settings.PriceMax, "Price maximum");
        ValidateScalar(settings.TopicFitMode, "Topic fit mode");
        ValidateScalar(settings.Quarantine, "Quarantine");
        ValidateOptionalMonth(settings.LastPublishedFromMonth, "Last publication from");
        ValidateOptionalMonth(settings.LastPublishedToMonth, "Last publication to");

        if (settings.TopicFitMode is not (TopicFitModeValues.Expand or TopicFitModeValues.Narrow))
        {
            throw new RequestValidationException("Topic fit mode must be expand or narrow.");
        }

        if (settings.Quarantine is not (
            QuarantineFilterValues.All or
            QuarantineFilterValues.Only or
            QuarantineFilterValues.Exclude))
        {
            throw new RequestValidationException("Quarantine must be all, only, or exclude.");
        }

        var normalizedStopListDomains = StopListParser.Parse(settings.StopListDomains);
        ValidateList(settings.ExcludedLocationKeys, "Excluded location keys");
        ValidateList(settings.Niches, "Niches");
        ValidateList(settings.CategorySearchTerms, "Category search terms");
        ValidateList(settings.ExcludedNiches, "Excluded niches");
        ValidateList(settings.ExcludedCategorySearchTerms, "Excluded category search terms");
        ValidateList(settings.Languages, "Languages");
        ValidateList(settings.CasinoAvailability, "Casino availability");
        ValidateList(settings.CryptoAvailability, "Crypto availability");
        ValidateList(settings.LinkInsertAvailability, "Link insert availability");
        ValidateList(settings.LinkInsertCasinoAvailability, "Link insert casino availability");
        ValidateList(settings.DatingAvailability, "Dating availability");
        ValidateLocationSelections(settings.LocationSelections);

        var normalizedSettings = NormalizeSettings(settings, normalizedStopListDomains);
        var json = JsonSerializer.Serialize(normalizedSettings, JsonOptions);
        if (json.Length > SavedFilterSetConstants.SettingsJsonMaxLength)
        {
            throw new RequestValidationException("Saved filter settings payload is too large.");
        }

        return json;
    }

    private static SavedFilterSettingsDto NormalizeSettings(
        SavedFilterSettingsDto settings,
        List<string>? normalizedStopListDomains)
        => new()
        {
            SchemaVersion = settings.SchemaVersion,
            StopListDomains = normalizedStopListDomains,
            DrMin = settings.DrMin,
            DrMax = settings.DrMax,
            TrafficMin = settings.TrafficMin,
            TrafficMax = settings.TrafficMax,
            PriceMin = settings.PriceMin,
            PriceMax = settings.PriceMax,
            LocationSelections = settings.LocationSelections,
            ExcludedLocationKeys = settings.ExcludedLocationKeys,
            Niches = settings.Niches,
            CategorySearchTerms = settings.CategorySearchTerms,
            TopicFitMode = settings.TopicFitMode,
            ExcludedNiches = settings.ExcludedNiches,
            ExcludedCategorySearchTerms = settings.ExcludedCategorySearchTerms,
            Languages = settings.Languages,
            CasinoAvailability = settings.CasinoAvailability,
            CryptoAvailability = settings.CryptoAvailability,
            LinkInsertAvailability = settings.LinkInsertAvailability,
            LinkInsertCasinoAvailability = settings.LinkInsertCasinoAvailability,
            DatingAvailability = settings.DatingAvailability,
            Quarantine = settings.Quarantine,
            LastPublishedFromMonth = settings.LastPublishedFromMonth,
            LastPublishedToMonth = settings.LastPublishedToMonth
        };

    private static void ValidateScalar(string? value, string label)
    {
        if (value == null)
        {
            throw new RequestValidationException($"{label} is required.");
        }

        if (value.Length > MaxScalarLength)
        {
            throw new RequestValidationException($"{label} is too long.");
        }
    }

    private static void ValidateOptionalScalar(string? value, string label, int maxLength)
    {
        if (value is { Length: > 0 } && value.Length > maxLength)
        {
            throw new RequestValidationException($"{label} is too long.");
        }
    }

    private static void ValidateOptionalMonth(string? value, string label)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (value.Length != 7 ||
            value[4] != '-' ||
            !int.TryParse(value.AsSpan(0, 4), out _) ||
            !int.TryParse(value.AsSpan(5, 2), out var month) ||
            month is < 1 or > 12)
        {
            throw new RequestValidationException($"{label} must use yyyy-MM format.");
        }
    }

    private static void ValidateList(
        IReadOnlyCollection<string?>? values,
        string label,
        int maxCount = 1000)
    {
        if (values == null)
        {
            return;
        }

        if (values.Count > maxCount)
        {
            throw new RequestValidationException($"{label} contains too many values.");
        }

        if (values.Any(value => value == null || value.Length > MaxListItemLength))
        {
            throw new RequestValidationException($"{label} contains a value that is too long.");
        }
    }

    private static void ValidateLocationSelections(
        IReadOnlyCollection<SavedFilterLocationSelectionDto>? selections)
    {
        if (selections == null)
        {
            throw new RequestValidationException("Location selections are required.");
        }

        if (selections.Count > 1000)
        {
            throw new RequestValidationException("Location selections contain too many values.");
        }

        foreach (var selection in selections)
        {
            ValidateScalar(selection.Kind, "Location selection kind");
            ValidateScalar(selection.Key, "Location selection key");
            ValidateScalar(selection.DisplayName, "Location selection display name");
            ValidateOptionalScalar(selection.GroupType, "Location group type", MaxScalarLength);
            ValidateOptionalScalar(selection.LocationKey, "Location key", MaxScalarLength);

            if (selection.Kind is not ("group" or "location" or "special"))
            {
                throw new RequestValidationException("Location selection kind is invalid.");
            }

            if (selection.LocationCount is < 0)
            {
                throw new RequestValidationException("Location count cannot be negative.");
            }

            if (selection.Locations.Count > 1000)
            {
                throw new RequestValidationException("Location group contains too many values.");
            }

            foreach (var location in selection.Locations)
            {
                ValidateScalar(location.Key, "Location key");
                ValidateScalar(location.DisplayName, "Location display name");
            }
        }
    }

    private static SavedFilterSetDto ToDto(UserSavedFilterSet filterSet)
        => new(
            filterSet.Id,
            filterSet.Name,
            filterSet.SchemaVersion,
            JsonSerializer.Deserialize<SavedFilterSettingsDto>(filterSet.SettingsJson, JsonOptions) ??
                throw new RequestValidationException("Stored saved filter settings are invalid."),
            filterSet.CreatedAtUtc,
            filterSet.UpdatedAtUtc);
}
