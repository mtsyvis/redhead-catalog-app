using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class ClientCatalogProtectionSettingsConfiguration : IEntityTypeConfiguration<ClientCatalogProtectionSettings>
{
    public void Configure(EntityTypeBuilder<ClientCatalogProtectionSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.ToTable("ClientCatalogProtectionSettings", table =>
        {
            table.HasCheckConstraint("CK_ClientCatalogProtectionSettings_Singleton", "\"Id\" = 1");
            table.HasCheckConstraint(
                "CK_ClientCatalogProtectionSettings_AutoBanThreshold_Positive",
                "\"AutoBanUniqueSitesPer24Hours\" > 0");
        });
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
    }
}
