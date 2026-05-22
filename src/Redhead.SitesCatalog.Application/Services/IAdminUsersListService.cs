using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

public interface IAdminUsersListService
{
    Task<AdminUsersListResult> ListUsersAsync(
        AdminUsersListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailsDto?> GetUserDetailsAsync(
        string id,
        CancellationToken cancellationToken = default);
}
