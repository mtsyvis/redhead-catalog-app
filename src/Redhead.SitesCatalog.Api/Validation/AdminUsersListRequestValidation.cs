using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Validation;

public static class AdminUsersListRequestValidation
{
    public static string? Validate(UserListRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserType))
        {
            return "Invalid userType. Allowed values: all, internal, clients, trusted-clients, lite.";
        }

        var userType = NormalizeUserType(request.UserType);
        if (userType is not (
            AdminUsersListUserTypes.All or
            AdminUsersListUserTypes.Internal or
            AdminUsersListUserTypes.Clients or
            AdminUsersListUserTypes.TrustedClients or
            AdminUsersListUserTypes.Lite))
        {
            return "Invalid userType. Allowed values: all, internal, clients, trusted-clients, lite.";
        }

        if (request.Page < 1)
        {
            return "Page must be greater than or equal to 1.";
        }

        if (!PaginationDefaults.PageSizes.Contains(request.PageSize))
        {
            return $"Invalid pageSize. Allowed values: {string.Join(", ", PaginationDefaults.PageSizes)}.";
        }

        return null;
    }

    public static string NormalizeUserType(string? userType)
    {
        return userType?.Trim().ToLowerInvariant() ?? AdminUsersListUserTypes.All;
    }
}

