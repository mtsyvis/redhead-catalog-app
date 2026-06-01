using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.AccountSetup;

public interface IAccountSetupService
{
    Task<AccountSetupCompletionResult> CompleteAsync(
        ApplicationUser user,
        CompleteAccountSetupRequest request);
}
