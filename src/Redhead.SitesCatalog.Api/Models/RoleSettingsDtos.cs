using System.ComponentModel.DataAnnotations;

namespace Redhead.SitesCatalog.Api.Models;

public record RoleSettingItemDto(string Role, int ExportLimitRows);

public record RoleSettingUpdateItemDto(
    [Required] [MinLength(1)] string Role,
    [Range(0, int.MaxValue)] int ExportLimitRows);
