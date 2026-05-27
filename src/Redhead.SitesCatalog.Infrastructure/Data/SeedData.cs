using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Locations;

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

            // Seed canonical locations and groups
            await SeedLocationsAsync(context, logger);
            await BackfillMissingSiteLocationsAsync(context, logger);

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

    private static async Task SeedLocationsAsync(ApplicationDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding canonical locations...");

        var (locationsAndGroups, aliases) = LocationSeedDataProvider.Load();
        LocationConfigValidator.Validate(locationsAndGroups, aliases);

        var existingLocations = await context.CanonicalLocations.ToDictionaryAsync(
            location => location.Key,
            StringComparer.Ordinal);
        foreach (var seedLocation in locationsAndGroups.Locations)
        {
            if (existingLocations.TryGetValue(seedLocation.Key, out var location))
            {
                location.DisplayName = seedLocation.DisplayName;
                location.SortOrder = seedLocation.SortOrder;
                location.IsActive = seedLocation.IsActive;
                continue;
            }

            context.CanonicalLocations.Add(new CanonicalLocation
            {
                Key = seedLocation.Key,
                DisplayName = seedLocation.DisplayName,
                SortOrder = seedLocation.SortOrder,
                IsActive = seedLocation.IsActive
            });
        }

        var existingGroups = await context.LocationGroups.ToDictionaryAsync(
            group => group.Key,
            StringComparer.Ordinal);
        foreach (var seedGroup in locationsAndGroups.Groups)
        {
            if (existingGroups.TryGetValue(seedGroup.Key, out var group))
            {
                group.DisplayName = seedGroup.DisplayName;
                group.Kind = seedGroup.Kind;
                group.SortOrder = seedGroup.SortOrder;
                continue;
            }

            context.LocationGroups.Add(new LocationGroup
            {
                Key = seedGroup.Key,
                DisplayName = seedGroup.DisplayName,
                Kind = seedGroup.Kind,
                SortOrder = seedGroup.SortOrder
            });
        }

        await context.SaveChangesAsync();

        var seedLocationKeys = locationsAndGroups.Locations
            .Select(location => location.Key)
            .ToArray();
        var desiredItems = locationsAndGroups.Groups
            .SelectMany(group => group.LocationKeys.Select(locationKey => new LocationGroupItem
            {
                GroupKey = group.Key,
                LocationKey = locationKey
            }))
            .ToList();
        var desiredItemKeys = desiredItems
            .Select(item => (item.GroupKey, item.LocationKey))
            .ToHashSet();
        var existingItems = await context.LocationGroupItems.ToListAsync();
        var existingItemKeys = existingItems
            .Select(item => (item.GroupKey, item.LocationKey))
            .ToHashSet();

        foreach (var staleItem in existingItems.Where(item => !desiredItemKeys.Contains((item.GroupKey, item.LocationKey))))
        {
            context.LocationGroupItems.Remove(staleItem);
        }

        foreach (var desiredItem in desiredItems.Where(item => !existingItemKeys.Contains((item.GroupKey, item.LocationKey))))
        {
            context.LocationGroupItems.Add(desiredItem);
        }

        await context.SaveChangesAsync();

        var staleLocations = await context.CanonicalLocations
            .Where(location => !seedLocationKeys.Contains(location.Key))
            .ToListAsync();
        foreach (var staleLocation in staleLocations)
        {
            var affectedSites = await context.Sites
                .Where(site => site.LocationKey == staleLocation.Key)
                .ToListAsync();
            foreach (var site in affectedSites)
            {
                site.ImportedLocationRaw ??= site.Location;
                site.LocationKey = null;
            }

            context.CanonicalLocations.Remove(staleLocation);
        }

        await context.SaveChangesAsync();

        var forbiddenLocations = await context.CanonicalLocations
            .AsNoTracking()
            .Where(location => location.DisplayName == "Other"
                               || location.Key == "OTHER")
            .Select(location => location.DisplayName)
            .ToListAsync();
        if (forbiddenLocations.Count > 0)
        {
            throw new SeedDataException(
                $"Forbidden canonical locations exist in the database: {string.Join(", ", forbiddenLocations)}");
        }
    }

    private static async Task BackfillMissingSiteLocationsAsync(ApplicationDbContext context, ILogger logger)
    {
        logger.LogInformation("Backfilling canonical site locations...");

        var normalizer = new LocationNormalizer(LocationSeedDataProvider.Load());
        var sites = await context.Sites
            .Where(site => site.LocationKey == null)
            .ToListAsync();

        if (sites.Count == 0)
        {
            logger.LogDebug("No sites require canonical location backfill.");
            return;
        }

        var updated = 0;
        foreach (var site in sites)
        {
            var rawLocation = site.ImportedLocationRaw ?? site.Location;
            var result = normalizer.Normalize(rawLocation);

            if (result.LocationKey == null && string.Equals(site.ImportedLocationRaw, rawLocation, StringComparison.Ordinal))
            {
                continue;
            }

            site.LocationKey = result.LocationKey;
            site.ImportedLocationRaw = result.Status == LocationNormalizationStatus.Unknown
                ? null
                : rawLocation;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Canonical site location backfill completed. Updated={Updated}", updated);
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
