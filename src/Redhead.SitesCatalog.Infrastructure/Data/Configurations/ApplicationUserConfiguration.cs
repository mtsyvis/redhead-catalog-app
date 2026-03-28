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
        });

        builder.Property(u => u.ExportLimitOverrideMode)
            .IsRequired(false)
            .HasConversion<int?>();

        builder.Property(u => u.ExportLimitRowsOverride)
            .IsRequired(false);
    }
}
