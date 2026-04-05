using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Middleware;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ISitesQueryBuilder, SitesQueryBuilder>();
builder.Services.AddScoped<ISitesService, SitesService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ISitesImportService, SitesImportService>();
builder.Services.AddScoped<IQuarantineImportService, QuarantineImportService>();
builder.Services.AddScoped<ILastPublishedImportService, LastPublishedImportService>();
builder.Services.AddScoped<ISitesUpdateImportService, SitesUpdateImportService>();
builder.Services.AddSingleton<IImportArtifactStorageService, ImportArtifactStorageService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    var regularSessionLifetime = TimeSpan.FromHours(8);
    var rememberMeSessionLifetime = TimeSpan.FromDays(7);

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.ExpireTimeSpan = regularSessionLifetime;
    options.SlidingExpiration = true;
    options.LoginPath = "/api/auth/login";
    options.LogoutPath = "/api/auth/logout";
    options.AccessDeniedPath = "/api/auth/access-denied";

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };

    // Extend cookie lifetime when "Remember me" is used (IsPersistent = true)
    options.Events.OnSigningIn = context =>
    {
        if (context.Properties.IsPersistent)
        {
            var issuedUtc = context.Properties.IssuedUtc ?? DateTimeOffset.UtcNow;
            context.Properties.IssuedUtc = issuedUtc;
            context.Properties.ExpiresUtc = issuedUtc.Add(rememberMeSessionLifetime);
        }

        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.SuperAdminOnly, policy =>
        policy.RequireRole(AppRoles.SuperAdmin));

    options.AddPolicy(AppPolicies.AdminAccess, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin));

    options.AddPolicy(AppPolicies.ReadOnlyAccess, policy =>
        policy.RequireRole(AppRoles.Internal, AppRoles.Client));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders(
                  "X-Export-Requested-Rows",
                  "X-Export-Exported-Rows",
                  "X-Export-Truncated",
                  "X-Export-Limit-Rows");
    });
});

var app = builder.Build();

// Seed database: roles and SuperAdmin (if not present) from configuration
await SeedData.InitializeAsync(app.Services);

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseMiddleware<RejectDisabledUserMiddleware>();
app.UseAuthorization();

app.MapControllers();

// SPA fallback for client-side routes from root
app.MapFallbackToFile("{*path:nonfile}", "index.html");

app.Run();
