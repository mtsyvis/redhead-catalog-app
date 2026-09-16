using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Integration;

public sealed class ClientCatalogSelectionConcurrencyTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(101)]
    public Task CatalogUsage_DoesNotConflictWithSelectionUpdate_AndResetsOnlyForTrustedLimit(int nextLimit)
    {
        var database = Guid.NewGuid().ToString();
        return VerifySelectionUpdateAsync(options => options.UseInMemoryDatabase(database), nextLimit);
    }

    // Shared with the opt-in PostgreSQL test to exercise selection updates with real Identity.
    internal static async Task VerifySelectionUpdateAsync(Action<DbContextOptionsBuilder> configureDatabase, int nextLimit)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(configureDatabase);
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var users = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = seedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(AppRoles.Client))).Succeeded);
            var user = new ApplicationUser { Id = "client", UserName = "client@example.com", Email = "client@example.com" };
            Assert.True((await users.CreateAsync(user)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Client)).Succeeded);
        }
        await using var adminScope = provider.CreateAsyncScope();
        var adminUsers = adminScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Admin reads the profile before the catalog request consumes the budget.
        Assert.NotNull(await adminUsers.FindByIdAsync("client"));
        var limiter = new ClientCatalogBurstLimiter(TimeProvider.System, 1000);
        await using (var catalogScope = provider.CreateAsyncScope())
        {
            var db = catalogScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var catalog = new ClientCatalogService(db, limiter);
            await catalog.EnsureBurstLimitAsync("client", Enumerable.Range(0, 1000).Select(i => $"{i}.com").ToArray(), 100);
        }
        var controller = new ClientSelectionLimitController(adminUsers, Mock.Of<IClientCatalogActivityService>(), limiter);

        // Act
        var response = await controller.Update("client", new ClientSelectionLimitController.UpdateRequest(nextLimit));

        // Assert
        Assert.IsType<NoContentResult>(response);
        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await verificationDb.Users.AsNoTracking().SingleAsync();
        Assert.Equal(nextLimit, updated.ClientSelectionLimitOverride);
        var nextRequest = Record.Exception(() => limiter.EnsureAllowed("client", ["next.com"], 100));
        if (nextLimit > 100) Assert.Null(nextRequest);
        else Assert.IsType<ClientCatalogBurstLimitExceededException>(nextRequest);
    }
}
