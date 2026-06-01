using Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class EmergencySitesExportServiceCollectionExtensions
{
    public static IServiceCollection AddEmergencySitesExportOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmergencySitesExportOptions>()
            .Bind(configuration.GetSection(EmergencySitesExportOptions.SectionName))
            .Validate(
                EmergencySitesExportOptions.IsValid,
                "EmergencySitesExport configuration is invalid. Enabled exports require ScheduleCron, GoogleDriveFolderId, ServiceAccountJsonPath, FilePrefix, and a positive RetentionWeeks value.")
            .Validate(
                options => !options.Enabled ||
                    EmergencySitesExportCronSchedule.TryParse(options.ScheduleCron, out _),
                "EmergencySitesExport configuration is invalid. ScheduleCron must be a parseable standard five-field cron expression when enabled.")
            .Validate(
                options => !options.Enabled || File.Exists(options.ServiceAccountJsonPath),
                "EmergencySitesExport configuration is invalid. ServiceAccountJsonPath must point to an existing service account JSON file when enabled.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddEmergencySitesExportHostedServiceIfEnabled(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(EmergencySitesExportOptions.SectionName)
            .Get<EmergencySitesExportOptions>();

        if (options?.Enabled == true)
        {
            services.AddSingleton<WeeklyEmergencySitesExportJob>();
            services.AddHostedService<WeeklyEmergencySitesExportHostedService>();
        }

        return services;
    }
}
