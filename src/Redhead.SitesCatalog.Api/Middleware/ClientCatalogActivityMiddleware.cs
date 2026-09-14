using System.Security.Claims;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Middleware;

public sealed class ClientCatalogActivityMiddleware(RequestDelegate next, ILogger<ClientCatalogActivityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory, TimeProvider clock)
    {
        var path = context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant();
        var tracked = context.User.IsInRole(AppRoles.Client) && HttpMethods.IsPost(context.Request.Method) &&
            path is "/api/sites/search" or "/api/sites/multi-search" or "/api/export/sites.xlsx" or
                "/api/export/sites-multi-search.xlsx" or "/api/sites/export/google-drive" or "/api/export/preview";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!tracked || userId is null)
        {
            await next(context);
            return;
        }

        var timestamp = clock.GetUtcNow().UtcDateTime;
        // Exception handling runs outside this middleware. Wait until it has produced
        // the final response before recording the status; use a fresh database scope.
        context.Response.OnCompleted(async () =>
        {
            // Activity collection must not turn a successful catalog response into an error.
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var status = context.Response.StatusCode;
                db.ClientCatalogRequests.Add(new ClientCatalogRequest
                {
                    UserId = userId,
                    TimestampUtc = timestamp,
                    Endpoint = path!,
                    StatusCode = status,
                    Domains = status is >= 200 and < 300 && context.Items["ClientCatalogDomains"] is string[] domains
                        ? domains.Distinct(StringComparer.Ordinal).ToArray() : []
                });
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await db.SaveChangesAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not persist Client catalog activity");
            }
        });
        await next(context);
    }
}
