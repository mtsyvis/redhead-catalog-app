using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogActivityCleanup;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.DependencyInjection;

public static class ClientCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddClientCatalogProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var requests = configuration.GetValue("ClientCatalog:RequestsPerMinute", 60);
        if (requests < 1)
        {
            throw new InvalidOperationException("ClientCatalog:RequestsPerMinute must be positive.");
        }

        services.AddScoped<IClientCatalogService, ClientCatalogService>();
        services.AddScoped<IClientCatalogActivityService, ClientCatalogActivityService>();
        services.AddHostedService<ClientCatalogActivityCleanupHostedService>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ClientCatalogLimits.RateLimitPolicy, context =>
                CreatePartition(context, requests));
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
