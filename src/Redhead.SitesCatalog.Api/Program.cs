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
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add application services
builder.Services.AddScoped<ISitesQueryBuilder, SitesQueryBuilder>();
builder.Services.AddScoped<ISitesService, SitesService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IImportFileParser, Redhead.SitesCatalog.Application.Services.Parsers.CsvSitesParser>();
builder.Services.AddScoped<ISitesImportService, SitesImportService>();
builder.Services.AddScoped<IQuarantineImportService, QuarantineImportService>();

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings (for production, keep strong; for dev, we can relax a bit)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // For dev, we don't need email confirmation
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest  // Allow HTTP in development
        : CookieSecurePolicy.Always;        // Require HTTPS in production
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
});

// Add Authorization policies
builder.Services.AddAuthorization(options =>
{
    // SuperAdmin only
    options.AddPolicy(AppPolicies.SuperAdminOnly, policy =>
        policy.RequireRole(AppRoles.SuperAdmin));

    // Admin access (SuperAdmin + Admin)
    options.AddPolicy(AppPolicies.AdminAccess, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin));

    // Read-only access (Internal + Client)
    options.AddPolicy(AppPolicies.ReadOnlyAccess, policy =>
        policy.RequireRole(AppRoles.Internal, AppRoles.Client));
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Important for cookie auth
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

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseMiddleware<RejectDisabledUserMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
