using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class RoleSettingsConfiguration : IEntityTypeConfiguration<RoleSettings>
{
    public void Configure(EntityTypeBuilder<RoleSettings> builder)
    {
        builder.ToTable("RoleSettings", t =>
        {
            t.HasCheckConstraint(
                "CK_RoleSettings_ExportLimitMode_ValidValue",
                @"""ExportLimitMode"" IN (1, 2, 3)");

            t.HasCheckConstraint(
                "CK_RoleSettings_ExportLimitRows_LimitedRequiresRows",
                @"""ExportLimitMode"" <> 2 OR (""ExportLimitRows"" IS NOT NULL AND ""ExportLimitRows"" > 0)");

            t.HasCheckConstraint(
                "CK_RoleSettings_ExportLimitRows_NonLimitedRequiresNullRows",
                @"""ExportLimitMode"" = 2 OR ""ExportLimitRows"" IS NULL");
        });

        builder.HasKey(rs => rs.RoleName);

        builder.Property(rs => rs.RoleName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(rs => rs.ExportLimitMode)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(rs => rs.ExportLimitRows)
            .IsRequired(false);

        // Seed default role settings with explicit mode-based values
        builder.HasData(
            new RoleSettings { RoleName = "SuperAdmin", ExportLimitMode = ExportLimitMode.Unlimited, ExportLimitRows = null },
            new RoleSettings { RoleName = "Admin", ExportLimitMode = ExportLimitMode.Unlimited, ExportLimitRows = null },
            new RoleSettings { RoleName = "Internal", ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 10000 },
            new RoleSettings { RoleName = "Client", ExportLimitMode = ExportLimitMode.Limited, ExportLimitRows = 5000 }
        );
    }
}
