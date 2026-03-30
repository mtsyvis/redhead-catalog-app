using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task CreateUser_WhenCurrentUserIsAdmin_ReturnsForbid()
    {
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin }
        };
        await using var db = CreateDbContext();
        var sut = new AdminUsersController(userManager, db, NullLogger<AdminUsersController>.Instance);

        var result = await sut.CreateUser(new CreateUserRequest("new-user@example.com", AppRoles.Client));

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Null(userManager.CreatedUser);
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserIsSuperAdmin_ReturnsOk()
    {
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "superadmin-1", Email = "superadmin@example.com" },
            CurrentRoles = new List<string> { AppRoles.SuperAdmin }
        };
        await using var db = CreateDbContext();
        var sut = new AdminUsersController(userManager, db, NullLogger<AdminUsersController>.Instance);

        var result = await sut.CreateUser(new CreateUserRequest("new-user@example.com", AppRoles.Client));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CreateUserResponse>(ok.Value);
        Assert.Equal("new-user@example.com", payload.Email);
        Assert.Equal(AppRoles.Client, payload.Role);
        Assert.False(string.IsNullOrWhiteSpace(payload.TemporaryPassword));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class StubUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUser? CurrentUser { get; init; }
        public IList<string> CurrentRoles { get; init; } = new List<string>();
        public ApplicationUser? ExistingUserByEmail { get; set; }
        public ApplicationUser? CreatedUser { get; private set; }

        public StubUserManager()
            : base(
                new StubUserStore(),
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!)
        {
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
            => Task.FromResult(CurrentUser);

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user)
            => Task.FromResult(CurrentRoles);

        public override Task<ApplicationUser?> FindByEmailAsync(string email)
            => Task.FromResult(ExistingUserByEmail);

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
        {
            user.Id = "created-user-1";
            CreatedUser = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
            => Task.FromResult(IdentityResult.Success);
    }

    private sealed class StubUserStore : IUserStore<ApplicationUser>
    {
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public void Dispose()
        {
        }
    }
}
