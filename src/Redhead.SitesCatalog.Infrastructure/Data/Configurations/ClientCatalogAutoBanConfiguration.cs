using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class ClientCatalogAutoBanConfiguration : IEntityTypeConfiguration<ClientCatalogAutoBan>
{
    public void Configure(EntityTypeBuilder<ClientCatalogAutoBan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("\"ReviewedAtUtc\" IS NULL");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
