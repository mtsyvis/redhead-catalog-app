using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.TableViews;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class UserTableViewsService : IUserTableViewsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SystemViewKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        TableViewConstants.DefaultSystemViewKey,
        "pricing",
        "seo",
        "full"
    };

    private readonly ApplicationDbContext _db;

    public UserTableViewsService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TableViewsResponseDto> GetTableViewsAsync(
        string userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var customViews = await _db.UserTableCustomViews
            .AsNoTracking()
            .Where(view => view.UserId == userId && view.TableKey == tableKey)
            .OrderBy(view => view.Name)
            .ToListAsync(cancellationToken);

        var preference = await _db.UserTablePreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == userId && item.TableKey == tableKey,
                cancellationToken);

        var activeViewType = TableViewConstants.SystemViewType;
        var activeViewKey = TableViewConstants.DefaultSystemViewKey;

        if (preference != null &&
            IsValidActiveView(preference.ActiveViewType, preference.ActiveViewKey, customViews))
        {
            activeViewType = preference.ActiveViewType;
            activeViewKey = preference.ActiveViewKey;
        }

        return new TableViewsResponseDto(
            activeViewType,
            activeViewKey,
            customViews.Select(ToDto).ToList());
    }

    public async Task SetActiveViewAsync(
        string userId,
        string tableKey,
        string viewType,
        string viewKey,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);
        var normalizedViewType = NormalizeViewType(viewType);
        var normalizedViewKey = ValidateAndNormalizeViewKey(normalizedViewType, viewKey);

        if (normalizedViewType == TableViewConstants.CustomViewType)
        {
            var customViewId = Guid.Parse(normalizedViewKey);
            var customViewExists = await _db.UserTableCustomViews.AnyAsync(
                view => view.UserId == userId && view.TableKey == tableKey && view.Id == customViewId,
                cancellationToken);

            if (!customViewExists)
            {
                throw new KeyNotFoundException("Custom view was not found.");
            }
        }

        var preference = await _db.UserTablePreferences.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);
        var now = DateTime.UtcNow;

        if (preference == null)
        {
            preference = new UserTablePreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TableKey = tableKey,
                CreatedAtUtc = now
            };
            _db.UserTablePreferences.Add(preference);
        }

        preference.ActiveViewType = normalizedViewType;
        preference.ActiveViewKey = normalizedViewKey;
        preference.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TableCustomViewDto> CreateCustomViewAsync(
        string userId,
        string tableKey,
        string name,
        TableViewSettingsDto settings,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var count = await _db.UserTableCustomViews.CountAsync(
            view => view.UserId == userId && view.TableKey == tableKey,
            cancellationToken);
        if (count >= TableViewConstants.CustomViewsPerUserTableLimit)
        {
            throw new RequestValidationException(
                $"A maximum of {TableViewConstants.CustomViewsPerUserTableLimit} custom views is allowed for this table.");
        }

        var normalizedName = await ValidateNameAsync(
            userId,
            tableKey,
            name,
            excludeId: null,
            cancellationToken);
        var settingsJson = ValidateAndSerializeSettings(settings);
        var now = DateTime.UtcNow;

        var view = new UserTableCustomView
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TableKey = tableKey,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            SchemaVersion = TableViewConstants.SchemaVersion,
            SettingsJson = settingsJson,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.UserTableCustomViews.Add(view);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(view);
    }

    public async Task<TableCustomViewDto> UpdateCustomViewAsync(
        string userId,
        string tableKey,
        Guid id,
        string? name,
        TableViewSettingsDto? settings,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var view = await _db.UserTableCustomViews.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);

        if (view == null)
        {
            throw new KeyNotFoundException("Custom view was not found.");
        }

        if (name != null)
        {
            view.NormalizedName = await ValidateNameAsync(
                userId,
                tableKey,
                name,
                id,
                cancellationToken);
            view.Name = name.Trim();
        }

        if (settings != null)
        {
            view.SettingsJson = ValidateAndSerializeSettings(settings);
            view.SchemaVersion = TableViewConstants.SchemaVersion;
        }

        view.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(view);
    }

    public async Task DeleteCustomViewAsync(
        string userId,
        string tableKey,
        Guid id,
        CancellationToken cancellationToken)
    {
        ValidateTableKey(tableKey);

        var view = await _db.UserTableCustomViews.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);
        if (view == null)
        {
            throw new KeyNotFoundException("Custom view was not found.");
        }

        _db.UserTableCustomViews.Remove(view);

        var preference = await _db.UserTablePreferences.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TableKey == tableKey,
            cancellationToken);

        if (preference != null &&
            preference.ActiveViewType == TableViewConstants.CustomViewType &&
            Guid.TryParse(preference.ActiveViewKey, out var activeCustomViewId) &&
            activeCustomViewId == id)
        {
            preference.ActiveViewType = TableViewConstants.SystemViewType;
            preference.ActiveViewKey = TableViewConstants.DefaultSystemViewKey;
            preference.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsValidActiveView(
        string activeViewType,
        string activeViewKey,
        IReadOnlyCollection<UserTableCustomView> customViews)
    {
        if (activeViewType == TableViewConstants.SystemViewType)
        {
            return SystemViewKeys.Contains(activeViewKey);
        }

        return activeViewType == TableViewConstants.CustomViewType &&
            customViews.Any(view => view.Id.ToString() == activeViewKey);
    }

    private static void ValidateTableKey(string tableKey)
    {
        if (!string.Equals(tableKey, TableViewConstants.SitesTableKey, StringComparison.Ordinal))
        {
            throw new RequestValidationException("Unsupported table key.");
        }
    }

    private static string NormalizeViewType(string viewType)
    {
        var normalized = viewType.Trim().ToLowerInvariant();
        if (normalized is not (TableViewConstants.SystemViewType or TableViewConstants.CustomViewType))
        {
            throw new RequestValidationException("View type must be system or custom.");
        }

        return normalized;
    }

    private static string ValidateAndNormalizeViewKey(string viewType, string viewKey)
    {
        var normalized = viewKey.Trim();
        if (normalized.Length == 0 || normalized.Length > 128)
        {
            throw new RequestValidationException("View key is required.");
        }

        if (viewType == TableViewConstants.SystemViewType)
        {
            normalized = normalized.ToLowerInvariant();
            if (!SystemViewKeys.Contains(normalized))
            {
                throw new RequestValidationException("Unknown system view.");
            }
        }
        else if (!Guid.TryParse(normalized, out _))
        {
            throw new RequestValidationException("Custom view key must be a valid id.");
        }

        return normalized;
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
            throw new RequestValidationException("Custom view name is required.");
        }

        if (trimmed.Length > TableViewConstants.CustomViewNameMaxLength)
        {
            throw new RequestValidationException(
                $"Custom view name must be {TableViewConstants.CustomViewNameMaxLength} characters or fewer.");
        }

        var normalizedName = trimmed.ToUpperInvariant();
        var duplicate = await _db.UserTableCustomViews.AnyAsync(
            view =>
                view.UserId == userId &&
                view.TableKey == tableKey &&
                view.NormalizedName == normalizedName &&
                (!excludeId.HasValue || view.Id != excludeId.Value),
            cancellationToken);

        if (duplicate)
        {
            throw new RequestValidationException("A custom view with this name already exists.");
        }

        return normalizedName;
    }

    private static string ValidateAndSerializeSettings(TableViewSettingsDto settings)
    {
        if (settings.SchemaVersion != TableViewConstants.SchemaVersion)
        {
            throw new RequestValidationException("Unsupported table view settings schema version.");
        }

        if (settings.VisibleColumnIds.Count == 0)
        {
            throw new RequestValidationException("At least one visible column is required.");
        }

        if (settings.VisibleColumnIds.Any(columnId => !IsValidColumnId(columnId)))
        {
            throw new RequestValidationException("Visible column ids contain an invalid value.");
        }

        if (settings.Density is not ("compact" or "standard" or "comfortable"))
        {
            throw new RequestValidationException("Density must be compact, standard, or comfortable.");
        }

        if (settings.ColumnWidths.Any(width =>
            !IsValidColumnId(width.Key) || width.Value < 40 || width.Value > 1000))
        {
            throw new RequestValidationException("Column widths contain an invalid value.");
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        if (json.Length > TableViewConstants.SettingsJsonMaxLength)
        {
            throw new RequestValidationException("Table view settings payload is too large.");
        }

        return json;
    }

    private static bool IsValidColumnId(string columnId)
        => columnId.Length is > 0 and <= 100 &&
            columnId.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-');

    private static TableCustomViewDto ToDto(UserTableCustomView view)
        => new(
            view.Id,
            view.Name,
            view.SchemaVersion,
            JsonSerializer.Deserialize<TableViewSettingsDto>(view.SettingsJson, JsonOptions) ??
                throw new RequestValidationException("Stored table view settings are invalid."),
            view.CreatedAtUtc,
            view.UpdatedAtUtc);
}
