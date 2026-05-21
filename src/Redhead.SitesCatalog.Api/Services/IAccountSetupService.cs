using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Services;

public interface IAccountSetupService
{
    Task<AccountSetupCompletionResult> CompleteAsync(
        ApplicationUser user,
        CompleteAccountSetupRequest request);
}
