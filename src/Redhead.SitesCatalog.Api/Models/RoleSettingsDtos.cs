using System.ComponentModel.DataAnnotations;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Api.Models;

public record RoleSettingItemDto(
    string Role,
    ExportLimitMode ExportLimitMode,
    int? ExportLimitRows,
    bool IsEditable);

public record RoleSettingUpdateItemDto(
    [Required][MinLength(1)] string Role,
    ExportLimitMode? ExportLimitMode,
    int? ExportLimitRows);
