using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogAlerts;
using Redhead.SitesCatalog.Api.DependencyInjection;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.DependencyInjection;

public sealed class ClientCatalogRegistrationTests
{
    [Fact]
    public void RegisteredLimiter_UsesConfiguredBudget()
    {
        // Arrange
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["ClientCatalog:RequestsPerMinute"] = "3",
            ["ClientCatalog:UniqueSitesPerFiveMinutes"] = "2"
        });
        var settings = provider.GetRequiredService<IOptions<ClientCatalogOptions>>().Value;
        var limiter = provider.GetRequiredService<ClientCatalogBurstLimiter>();

        // Act
        var belowLimit = Record.Exception(() => limiter.EnsureAllowed("client", ["a.example"], 1));
        var atLimit = Record.Exception(() => limiter.EnsureAllowed("client", ["b.example"], 1));
        var blocked = Record.Exception(() => limiter.EnsureAllowed("client", ["c.example"], 1));

        // Assert
        Assert.Equal(3, settings.RequestsPerMinute);
        Assert.Null(belowLimit);
        Assert.Null(atLimit);
        Assert.Equal(300, Assert.IsType<ClientCatalogBurstLimitExceededException>(blocked).RetryAfterSeconds);
    }

    [Theory]
    [InlineData("RequestsPerMinute", "0")]
    [InlineData("RequestsPerMinute", "-1")]
    [InlineData("UniqueSitesPerFiveMinutes", "0")]
    [InlineData("UniqueSitesPerFiveMinutes", "-1")]
    public void RegisteredLimiter_RejectsInvalidOptions(string setting, string value)
    {
        // Arrange
        using var provider = CreateProvider(new Dictionary<string, string?> { [$"ClientCatalog:{setting}"] = value });

        // Act
        var exception = Record.Exception(() => provider.GetRequiredService<ClientCatalogBurstLimiter>());

        // Assert
        Assert.IsType<OptionsValidationException>(exception);
    }

    [Fact]
    public void Registration_UsesIndependentAlertAndAutoBanHostedServices()
    {
        // Arrange
        using var provider = CreateProvider([]);

        // Act
        var hostedServiceTypes = provider.GetServices<IHostedService>()
            .Select(service => service.GetType())
            .ToList();

        // Assert
        Assert.Contains(typeof(ClientCatalogAlertHostedService), hostedServiceTypes);
        Assert.Contains(typeof(ClientCatalogAutoBanHostedService), hostedServiceTypes);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddClientCatalogProtection(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
