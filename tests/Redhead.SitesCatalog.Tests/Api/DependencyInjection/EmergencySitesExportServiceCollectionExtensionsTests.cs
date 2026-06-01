using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;
using Redhead.SitesCatalog.Api.DependencyInjection;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.DependencyInjection;

public sealed class EmergencySitesExportServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEmergencySitesExportHostedServiceIfEnabled_WhenDisabled_DoesNotRegisterHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(enabled: false);

        // Act
        services.AddEmergencySitesExportHostedServiceIfEnabled(configuration);

        // Assert
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddEmergencySitesExportHostedServiceIfEnabled_WhenEnabled_RegistersHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(enabled: true);

        // Act
        services.AddEmergencySitesExportHostedServiceIfEnabled(configuration);

        // Assert
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(WeeklyEmergencySitesExportHostedService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(WeeklyEmergencySitesExportJob) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    private static IConfiguration CreateConfiguration(bool enabled)
    {
        var values = new Dictionary<string, string?>
        {
            [$"{EmergencySitesExportOptions.SectionName}:Enabled"] = enabled.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
