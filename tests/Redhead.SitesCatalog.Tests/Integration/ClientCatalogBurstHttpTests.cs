using Redhead.SitesCatalog.Domain.ClientCatalog;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.DependencyInjection;
using Redhead.SitesCatalog.Api.Middleware;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

public sealed class ClientCatalogBurstHttpTests
{
    [Theory]
    [InlineData(101)]
    [InlineData(5000)]
    public async Task HttpSearch_SharesRollingBudgetAcrossSessions_AllowsRepeats_AndRecovers(int trustedSelectionLimit)
    {
        // Arrange
        var clock = new MutableClock();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddClientCatalogProtection(builder.Configuration);
        builder.Services.AddSingleton(Mock.Of<IClientCatalogAlertEmailSender>());
        builder.Services.AddControllers().AddApplicationPart(typeof(SitesController).Assembly);
        builder.Services.AddAuthorization(options => options.AddPolicy(AppPolicies.SitesBrowseAccess,
            policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton(Mock.Of<ILiteMultiSearchUsageService>());
        var sites = new Mock<ISitesService>();
        sites.Setup(service => service.GetSitesAsync(It.IsAny<SitesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SitesQuery query, CancellationToken _) => new SitesListResult
            {
                Items = Enumerable.Range(1, 100).Select(i => new SiteDto { Domain = $"{query.Search}-{i}.com" }).ToList(),
                Total = 100
            });
        builder.Services.AddSingleton(sites.Object);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        // Test identity only; real controllers, limiter registration and exception responses.
        app.Use((context, next) =>
        {
            var userId = context.Request.Headers["X-Test-User"].FirstOrDefault() ?? "client";
            context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, AppRoles.Client)], "Test"));
            return next(context);
        });
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.AddRange(new ApplicationUser { Id = "client" }, new ApplicationUser { Id = "other-client" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        using var firstSession = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var secondSession = new HttpClient { BaseAddress = firstSession.BaseAddress };
        using var otherUser = new HttpClient { BaseAddress = firstSession.BaseAddress };
        otherUser.DefaultRequestHeaders.Add("X-Test-User", "other-client");

        // Act
        var allowed = new List<HttpStatusCode>();
        for (var i = 0; i < 20; i++)
        {
            using var response = await firstSession.PostAsJsonAsync("/api/sites/search", new { search = $"batch{i}" });
            allowed.Add(response.StatusCode);
        }
        using var blocked = await secondSession.PostAsJsonAsync("/api/sites/search", new { search = "new" });
        using var blockedBody = await JsonDocument.ParseAsync(await blocked.Content.ReadAsStreamAsync());
        using var repeated = await secondSession.PostAsJsonAsync("/api/sites/search", new { search = "batch0" });
        using var independent = await otherUser.PostAsJsonAsync("/api/sites/search", new { search = "new" });
        clock.Advance(TimeSpan.FromMinutes(5));
        using var recovered = await firstSession.PostAsJsonAsync("/api/sites/search", new { search = "new" });

        // A trusted personal limit bypasses only the five-minute data budget.
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(user => user.Id == "client");
            user.ClientSelectionLimitOverride = trustedSelectionLimit;
            await db.SaveChangesAsync();
        }
        var trustedStatuses = new List<HttpStatusCode>();
        for (var i = 0; i < 24; i++)
        {
            using var response = await firstSession.PostAsJsonAsync("/api/sites/search", new { search = $"trusted{i}" });
            trustedStatuses.Add(response.StatusCode);
        }

        // Assert
        Assert.All(allowed, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("ClientCatalogBurstLimited", blockedBody.RootElement.GetProperty("code").GetString());
        Assert.False(blockedBody.RootElement.TryGetProperty("items", out _));
        Assert.Equal(TimeSpan.FromMinutes(5), blocked.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.All(trustedStatuses, code => Assert.Equal(HttpStatusCode.OK, code));
    }

    private sealed class MutableClock : TimeProvider
    {
        private long _ticks = new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero).Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }
}
