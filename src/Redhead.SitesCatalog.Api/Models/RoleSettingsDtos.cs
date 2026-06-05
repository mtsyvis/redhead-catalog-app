using System.ComponentModel.DataAnnotations;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record RoleSettingItemDto(
    string Role,
    ExportLimitMode ExportLimitMode,
    int? ExportLimitRows,
    bool IsEditable,
    int? DailyUniqueExportedDomainsLimit = null,
    int? WeeklyUniqueExportedDomainsLimit = null,
    int? DailyExportOperationsLimit = null,
    int? WeeklyExportOperationsLimit = null);

public sealed class RoleSettingUpdateItemDto
{
    public RoleSettingUpdateItemDto()
    {
    }

    public RoleSettingUpdateItemDto(
        string role,
        ExportLimitMode? exportLimitMode,
        int? exportLimitRows)
    {
        Role = role;
        ExportLimitMode = exportLimitMode;
        ExportLimitRows = exportLimitRows;
    }

    [Required]
    [MinLength(1)]
    public string Role { get; set; } = string.Empty;

    public ExportLimitMode? ExportLimitMode { get; set; }

    public int? ExportLimitRows { get; set; }

    public int? DailyUniqueExportedDomainsLimit { get; set; }

    public int? WeeklyUniqueExportedDomainsLimit { get; set; }

    public int? DailyExportOperationsLimit { get; set; }

    public int? WeeklyExportOperationsLimit { get; set; }
}
