using Redhead.SitesCatalog.Api.BackgroundJobs.ExportedDomainAccessCleanup;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class ExportedDomainAccessCleanupServiceCollectionExtensions
{
    public static IServiceCollection AddExportedDomainAccessCleanup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ExportedDomainAccessCleanupOptions>()
            .Bind(configuration.GetSection(ExportedDomainAccessCleanupOptions.SectionName))
            .Validate(
                ExportedDomainAccessCleanupOptions.IsValid,
                "ExportedDomainAccessCleanup configuration is invalid. Enabled cleanup requires RetentionDays >= 7, positive BatchSize, and positive IntervalHours.")
            .ValidateOnStart();
        services.AddScoped<IExportedDomainAccessCleanupService, ExportedDomainAccessCleanupService>();

        var options = configuration
            .GetSection(ExportedDomainAccessCleanupOptions.SectionName)
            .Get<ExportedDomainAccessCleanupOptions>() ?? new ExportedDomainAccessCleanupOptions();

        if (options.Enabled)
        {
            services.AddHostedService<ExportedDomainAccessCleanupHostedService>();
        }

        return services;
    }
}
