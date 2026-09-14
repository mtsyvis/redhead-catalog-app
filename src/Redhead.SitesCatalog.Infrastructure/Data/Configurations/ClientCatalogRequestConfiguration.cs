using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class ClientCatalogRequestConfiguration : IEntityTypeConfiguration<ClientCatalogRequest>
{
    public void Configure(EntityTypeBuilder<ClientCatalogRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Endpoint).HasMaxLength(100);
        builder.Property(x => x.Domains).HasColumnType("text[]");
        builder.HasIndex(x => new { x.UserId, x.TimestampUtc });
        builder.HasIndex(x => x.TimestampUtc);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
