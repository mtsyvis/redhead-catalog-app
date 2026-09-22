using Redhead.SitesCatalog.Domain.ClientCatalog;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogActivityCleanup;
using Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogAlerts;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Options;
using Redhead.SitesCatalog.Infrastructure.Email;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class ClientCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddClientCatalogProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClientCatalogOptions>().Bind(configuration.GetSection(ClientCatalogOptions.SectionName))
            .Validate(ClientCatalogOptions.IsValid, "Client catalog settings require positive limits and valid alert email addresses.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<ClientCatalogOptions>>().Value;
            return new ClientCatalogBurstLimiter(provider.GetRequiredService<TimeProvider>(), settings.UniqueSitesPerFiveMinutes);
        });
        services.AddScoped<IClientCatalogAlertEmailSender, ClientCatalogAlertEmailSender>();
        services.AddScoped<ClientCatalogAlertService>();
        services.AddScoped<ClientCatalogAutoBanService>();
        services.AddHostedService<ClientCatalogAlertHostedService>();
        services.AddHostedService<ClientCatalogAutoBanHostedService>();
        services.AddScoped<IClientCatalogService, ClientCatalogService>();
        services.AddScoped<IClientCatalogActivityService, ClientCatalogActivityService>();
        services.AddHostedService<ClientCatalogActivityCleanupHostedService>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ClientCatalogLimits.RateLimitPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<ClientCatalogOptions>>().Value;
                return CreatePartition(context, settings.RequestsPerMinute);
            });
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                rejected.HttpContext.Response.Headers.RetryAfter = "60";
                await rejected.HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "ClientCatalogRateLimited",
                    message = "Too many catalog requests. Please wait a minute and try again. Your selection has been preserved.",
                    retryAfterSeconds = 60
                }, cancellationToken);
            };
        });
        return services;
    }

    public static RateLimitPartition<string> CreatePartition(HttpContext context, int requests)
        => context.User.IsInRole(AppRoles.Client)
            ? RateLimitPartition.GetSlidingWindowLimiter(
                "client:" + context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = requests,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true
                })
            : RateLimitPartition.GetNoLimiter("internal");
}
