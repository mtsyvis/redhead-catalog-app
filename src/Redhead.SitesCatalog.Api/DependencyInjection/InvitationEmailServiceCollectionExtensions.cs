using MailKit.Net.Smtp;
using Redhead.SitesCatalog.Application.Invitations;
using Redhead.SitesCatalog.Domain.Invitations;
using Redhead.SitesCatalog.Infrastructure.Email;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class InvitationEmailServiceCollectionExtensions
{
    public static IServiceCollection AddInvitationEmail(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .Validate(FrontendOptions.IsValid, "Frontend BaseUrl must be an absolute root HTTP or HTTPS URL.")
            .Validate(
                options => !environment.IsProduction() ||
                    Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) &&
                    baseUri.Scheme == Uri.UriSchemeHttps,
                "Frontend BaseUrl must use HTTPS in Production.")
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(
                EmailOptions.IsValid,
                "Email configuration is invalid. Enabled email requires SMTP host, valid port, From name/address, and a timeout from 1 to 60 seconds.")
            .ValidateOnStart();

        services.AddScoped<ISmtpClient>(_ => new SmtpClient());
        services.AddScoped<IInvitationEmailSender, MailKitInvitationEmailSender>();
        services.AddScoped<IInvitationDeliveryService, InvitationDeliveryService>();

        return services;
    }
}
