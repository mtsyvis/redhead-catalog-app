using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers", t =>
        {
            t.HasCheckConstraint(
                "CK_AspNetUsers_ExportLimitOverride_ValidMode",
                @"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" IN (1, 2, 3)");

            t.HasCheckConstraint(
                "CK_AspNetUsers_ExportLimitOverride_LimitedRequiresRows",
                @"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" <> 2 OR (""ExportLimitRowsOverride"" IS NOT NULL AND ""ExportLimitRowsOverride"" > 0)");

            t.HasCheckConstraint(
                "CK_AspNetUsers_ExportLimitOverride_NonLimitedRequiresNullRows",
                @"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" = 2 OR ""ExportLimitRowsOverride"" IS NULL");

            t.HasCheckConstraint(
                "CK_AspNetUsers_ExportLimitOverride_NullModeRequiresNullRows",
                @"""ExportLimitOverrideMode"" IS NOT NULL OR ""ExportLimitRowsOverride"" IS NULL");

            t.HasCheckConstraint(
                "CK_AspNetUsers_DailyUniqueExportedDomainsLimitOverride_PositiveOrNull",
                @"""DailyUniqueExportedDomainsLimitOverride"" IS NULL OR ""DailyUniqueExportedDomainsLimitOverride"" > 0");

            t.HasCheckConstraint(
                "CK_AspNetUsers_WeeklyUniqueExportedDomainsLimitOverride_PositiveOrNull",
                @"""WeeklyUniqueExportedDomainsLimitOverride"" IS NULL OR ""WeeklyUniqueExportedDomainsLimitOverride"" > 0");

            t.HasCheckConstraint(
                "CK_AspNetUsers_DailyExportOperationsLimitOverride_PositiveOrNull",
                @"""DailyExportOperationsLimitOverride"" IS NULL OR ""DailyExportOperationsLimitOverride"" > 0");

            t.HasCheckConstraint(
                "CK_AspNetUsers_WeeklyExportOperationsLimitOverride_PositiveOrNull",
                @"""WeeklyExportOperationsLimitOverride"" IS NULL OR ""WeeklyExportOperationsLimitOverride"" > 0");
        });

        builder.Property(u => u.ExportLimitOverrideMode)
            .IsRequired(false)
            .HasConversion<int?>();

        builder.Property(u => u.ExportLimitRowsOverride)
            .IsRequired(false);

        builder.Property(u => u.DailyUniqueExportedDomainsLimitOverride)
            .IsRequired(false);

        builder.Property(u => u.WeeklyUniqueExportedDomainsLimitOverride)
            .IsRequired(false);

        builder.Property(u => u.DailyExportOperationsLimitOverride)
            .IsRequired(false);

        builder.Property(u => u.WeeklyExportOperationsLimitOverride)
            .IsRequired(false);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.ActivatedAtUtc)
            .IsRequired(false);

        builder.Property(u => u.InvitationTokenHash)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.HasIndex(u => u.InvitationTokenHash)
            .IsUnique();

        builder.Property(u => u.InvitationExpiresAtUtc)
            .IsRequired(false);

        builder.Property(u => u.SuperAdminNote)
            .HasMaxLength(1000)
            .IsRequired(false);
    }
}
