using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Redhead.SitesCatalog.Api.DependencyInjection;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Api.DependencyInjection;

public sealed class InvitationEmailServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInvitationEmail_WhenEnabledConfigurationIsInvalid_FailsOptionsValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Frontend:BaseUrl"] = "https://catalog.rhda.us",
            ["Email:Enabled"] = "true"
        });
        services.AddInvitationEmail(configuration, CreateEnvironment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => provider.GetRequiredService<IOptions<EmailOptions>>().Value;

        // Assert
        Assert.Throws<OptionsValidationException>(act);
    }

    [Fact]
    public void AddInvitationEmail_WhenEmailIsDisabled_DoesNotRequireSmtpSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Frontend:BaseUrl"] = "http://localhost:5173",
            ["Email:Enabled"] = "false"
        });
        services.AddInvitationEmail(configuration, CreateEnvironment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;

        // Assert
        Assert.False(options.Enabled);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
