using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class ClientCatalogAlertConfiguration : IEntityTypeConfiguration<ClientCatalogAlert>
{
    public void Configure(EntityTypeBuilder<ClientCatalogAlert> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("\"ReviewedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
