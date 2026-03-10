using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Exceptions;

namespace Redhead.SitesCatalog.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            logger.LogInformation("Starting database seeding...");

            // Ensure database is created and migrations are applied
            await context.Database.MigrateAsync();

            // Seed roles
            await SeedRolesAsync(roleManager, logger);

            // Seed SuperAdmin user
            await SeedSuperAdminAsync(userManager, configuration, logger);

            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        logger.LogInformation("Seeding roles...");

        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Creating role: {RoleName}", roleName);
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, errors);
                    throw new SeedDataException($"Failed to create role {roleName}: {errors}");
                }
            }
            else
            {
                logger.LogDebug("Role already exists: {RoleName}", roleName);
            }
        }
    }

    private static async Task SeedSuperAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("Seeding SuperAdmin user...");

        // Get credentials from configuration with fallback for development
        var superAdminEmail = configuration["SeedData:SuperAdmin:Email"] ?? "superadmin@redhead.com";
        var superAdminPassword = configuration["SeedData:SuperAdmin:Password"];

        if (string.IsNullOrEmpty(superAdminPassword))
        {
            // For development only - in production this should be in configuration
            superAdminPassword = "SuperAdmin123!";
            logger.LogWarning("SuperAdmin password not found in configuration. Using default password for development. " +
                            "DO NOT USE IN PRODUCTION! Set SeedData:SuperAdmin:Password in configuration.");
        }

        var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);

        if (superAdmin == null)
        {
            logger.LogInformation("Creating SuperAdmin user: {Email}", superAdminEmail);

            superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = false // Dev user doesn't need to change password
            };

            var result = await userManager.CreateAsync(superAdmin, superAdminPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create SuperAdmin user: {Errors}", errors);
                throw new SeedDataException($"Failed to create SuperAdmin user: {errors}");
            }

            result = await userManager.AddToRoleAsync(superAdmin, AppRoles.SuperAdmin);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to assign SuperAdmin role: {Errors}", errors);
                throw new SeedDataException($"Failed to assign SuperAdmin role: {errors}");
            }

            logger.LogInformation("SuperAdmin user created successfully");
        }
        else
        {
            logger.LogDebug("SuperAdmin user already exists: {Email}", superAdminEmail);
        }
    }
}
