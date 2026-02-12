using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class RoleSettingsConfiguration : IEntityTypeConfiguration<RoleSettings>
{
    public void Configure(EntityTypeBuilder<RoleSettings> builder)
    {
        builder.ToTable("RoleSettings");

        builder.HasKey(rs => rs.RoleName);

        builder.Property(rs => rs.RoleName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(rs => rs.ExportLimitRows)
            .IsRequired();

        // Seed default role settings
        builder.HasData(
            new RoleSettings { RoleName = "Admin", ExportLimitRows = 1000000 },
            new RoleSettings { RoleName = "UserManager", ExportLimitRows = 0 },
            new RoleSettings { RoleName = "Editor", ExportLimitRows = 10000 },
            new RoleSettings { RoleName = "Viewer", ExportLimitRows = 5000 },
            new RoleSettings { RoleName = "Client", ExportLimitRows = 1000 }
        );
    }
}
