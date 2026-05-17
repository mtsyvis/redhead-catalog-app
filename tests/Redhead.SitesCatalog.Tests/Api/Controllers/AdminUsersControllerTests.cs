using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task ListUsers_WithDefaultRequest_ReturnsFirstPageWithDefaultPageSize()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        for (var i = 1; i <= 30; i++)
        {
            await AddUserAsync(db, $"client-{i:00}", $"client-{i:00}@example.com", AppRoles.Client);
        }

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest(), CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(1, payload.Page);
        Assert.Equal(25, payload.PageSize);
        Assert.Equal(30, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Equal(25, payload.Items.Count);
        Assert.Equal("client-01@example.com", payload.Items[0].Email);
        Assert.Equal("client-25@example.com", payload.Items[^1].Email);
    }

    [Fact]
    public async Task ListUsers_WithAllFilter_ReturnsAllUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "all" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(4, payload.TotalCount);
        Assert.Equal(
            [AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Internal, AppRoles.Client],
            payload.Items.Select(item => item.Role));
    }

    [Fact]
    public async Task ListUsers_WithClientsFilter_ReturnsOnlyClientUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "client-1", "client-a@example.com", AppRoles.Client);
        await AddUserAsync(db, "client-2", "client-b@example.com", AppRoles.Client);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "clients" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(2, payload.TotalCount);
        Assert.All(payload.Items, item => Assert.Equal(AppRoles.Client, item.Role));
    }

    [Fact]
    public async Task ListUsers_WithInternalFilter_ReturnsNonClientUsers()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);
        await AddUserAsync(db, "future-1", "future-role@example.com", "FutureInternal");
        await AddUserAsync(db, "client-1", "client@example.com", AppRoles.Client);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { UserType = "internal" }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(4, payload.TotalCount);
        Assert.DoesNotContain(payload.Items, item => item.Role == AppRoles.Client);
        Assert.Contains(payload.Items, item => item.Role == "FutureInternal");
    }

    [Fact]
    public async Task ListUsers_WithPagination_ReturnsCorrectItemsAndCounts()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        for (var i = 1; i <= 12; i++)
        {
            await AddUserAsync(db, $"client-{i:00}", $"client-{i:00}@example.com", AppRoles.Client);
        }

        var sut = CreateController(db);

        var result = await sut.ListUsers(
            new UserListRequest { UserType = "clients", Page = 2, PageSize = 10 },
            CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(2, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(12, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Equal(["client-11@example.com", "client-12@example.com"], payload.Items.Select(item => item.Email));
    }

    [Theory]
    [InlineData("unknown", 1, 25, "Invalid userType")]
    [InlineData("", 1, 25, "Invalid userType")]
    [InlineData("all", 0, 25, "Page must be greater than or equal to 1.")]
    [InlineData("all", 1, 15, "Invalid pageSize")]
    public async Task ListUsers_WithInvalidQuery_ReturnsBadRequest(
        string userType,
        int page,
        int pageSize,
        string expectedMessage)
    {
        await using var db = CreateDbContext();
        var sut = CreateController(db);

        var result = await sut.ListUsers(
            new UserListRequest { UserType = userType, Page = page, PageSize = pageSize },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Contains(expectedMessage, payload.Message);
    }

    [Fact]
    public async Task ListUsers_OrdersBeforePaginationUsingStableOrdering()
    {
        await using var db = CreateDbContext();
        await SeedRoleSettingsAsync(db);
        await AddUserAsync(db, "client-2", "same@example.com", AppRoles.Client);
        await AddUserAsync(db, "admin-1", "admin@example.com", AppRoles.Admin);
        await AddUserAsync(db, "client-1", "same@example.com", AppRoles.Client);
        await AddUserAsync(db, "disabled-superadmin-1", "superadmin@example.com", AppRoles.SuperAdmin, isActive: false);
        await AddUserAsync(db, "internal-1", "internal@example.com", AppRoles.Internal);

        var sut = CreateController(db);

        var result = await sut.ListUsers(new UserListRequest { Page = 1, PageSize = 10 }, CancellationToken.None);

        var payload = GetOkPayload(result);
        Assert.Equal(
            ["admin-1", "internal-1", "client-1", "client-2", "disabled-superadmin-1"],
            payload.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task CreateUser_WhenCurrentUserIsAdmin_ReturnsForbid()
    {
        var userManager = new StubUserManager
        {
            CurrentUser = new ApplicationUser { Id = "admin-1", Email = "admin@example.com" },
            CurrentRoles = new List<string> { AppRoles.Admin }
        };
        await using var db = CreateDbContext();
        var sut = CreateController(db, userManager);

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
        var sut = CreateController(db, userManager);

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

    private static AdminUsersController CreateController(ApplicationDbContext db)
    {
        return CreateController(db, new StubUserManager());
    }

    private static AdminUsersController CreateController(
        ApplicationDbContext db,
        StubUserManager userManager)
    {
        return new AdminUsersController(
            userManager,
            new AdminUsersListService(db),
            NullLogger<AdminUsersController>.Instance);
    }

    private static UserListResponse GetOkPayload(ActionResult<UserListResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<UserListResponse>(ok.Value);
    }

    private static async Task SeedRoleSettingsAsync(ApplicationDbContext db)
    {
        db.RoleSettings.AddRange(
            new RoleSettings { RoleName = AppRoles.SuperAdmin, ExportLimitMode = ExportLimitMode.Unlimited },
            new RoleSettings { RoleName = AppRoles.Admin, ExportLimitMode = ExportLimitMode.Unlimited },
            new RoleSettings { RoleName = AppRoles.Internal, ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 1000 },
            new RoleSettings { RoleName = AppRoles.Client, ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 100 });

        await db.SaveChangesAsync();
    }

    private static async Task AddUserAsync(
        ApplicationDbContext db,
        string id,
        string email,
        string roleName,
        bool isActive = true)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
        if (role == null)
        {
            role = new IdentityRole
            {
                Id = $"role-{roleName}",
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };
            db.Roles.Add(role);
        }

        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = isActive
        });

        db.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = id,
            RoleId = role.Id
        });

        await db.SaveChangesAsync();
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
