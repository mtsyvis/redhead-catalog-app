using Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class AhrefsSyncServiceCollectionExtensions
{
    public static IServiceCollection AddAhrefsSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AhrefsOptions>()
            .Bind(configuration.GetSection(AhrefsOptions.SectionName))
            .Validate(AhrefsOptions.IsValid, "Ahrefs BaseUrl must be an absolute HTTPS URL.")
            .ValidateOnStart();
        services.AddOptions<AhrefsSyncOptions>()
            .Bind(configuration.GetSection(AhrefsSyncOptions.SectionName))
            .Validate(
                AhrefsSyncOptions.IsValid,
                "AhrefsSync configuration is invalid.")
            .Validate(
                options => !options.Enabled ||
                    AhrefsSyncCronSchedule.TryParse(options.Cron, out _),
                "AhrefsSync Cron must be a standard five-field cron expression.")
            .ValidateOnStart();

        var options = configuration
            .GetSection(AhrefsSyncOptions.SectionName)
            .Get<AhrefsSyncOptions>();
        if (options?.Enabled == true)
        {
            services.AddHostedService<AhrefsSyncHostedService>();
        }

        return services;
    }
}
