using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<RoleSettings> RoleSettings => Set<RoleSettings>();
    public DbSet<ExportLog> ExportLogs => Set<ExportLog>();
    public DbSet<ExportedDomainAccess> ExportedDomainAccesses => Set<ExportedDomainAccess>();
    public DbSet<ExportAnalyticsSnapshot> ExportAnalyticsSnapshots => Set<ExportAnalyticsSnapshot>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<GoogleDriveConnection> GoogleDriveConnections => Set<GoogleDriveConnection>();
    public DbSet<UserTablePreference> UserTablePreferences => Set<UserTablePreference>();
    public DbSet<UserTableCustomView> UserTableCustomViews => Set<UserTableCustomView>();
    public DbSet<UserSavedFilterSet> UserSavedFilterSets => Set<UserSavedFilterSet>();
    public DbSet<CanonicalLocation> CanonicalLocations => Set<CanonicalLocation>();
    public DbSet<LocationGroup> LocationGroups => Set<LocationGroup>();
    public DbSet<LocationGroupItem> LocationGroupItems => Set<LocationGroupItem>();
    public DbSet<SystemJobRun> SystemJobRuns => Set<SystemJobRun>();
    public DbSet<SystemJobArtifact> SystemJobArtifacts => Set<SystemJobArtifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
