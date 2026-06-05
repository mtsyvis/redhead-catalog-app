using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Services;

public interface IEffectiveExportPolicyService
{
    Task<EffectiveExportPolicy> GetEffectivePolicyAsync(
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<EffectiveExportPolicy> GetEffectivePolicyAsync(
        ApplicationUser? user,
        string userRole,
        CancellationToken cancellationToken = default);
}
