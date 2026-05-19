using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Api.Validation;

public static class AdminUsersListRequestValidation
{
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];

    public static string? Validate(UserListRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserType))
        {
            return "Invalid userType. Allowed values: all, internal, clients.";
        }

        var userType = NormalizeUserType(request.UserType);
        if (userType is not (
            AdminUsersListUserTypes.All or
            AdminUsersListUserTypes.Internal or
            AdminUsersListUserTypes.Clients))
        {
            return "Invalid userType. Allowed values: all, internal, clients.";
        }

        if (request.Page < 1)
        {
            return "Page must be greater than or equal to 1.";
        }

        if (!AllowedPageSizes.Contains(request.PageSize))
        {
            return "Invalid pageSize. Allowed values: 10, 25, 50, 100.";
        }

        return null;
    }

    public static string NormalizeUserType(string? userType)
    {
        return userType?.Trim().ToLowerInvariant() ?? AdminUsersListUserTypes.All;
    }
}

