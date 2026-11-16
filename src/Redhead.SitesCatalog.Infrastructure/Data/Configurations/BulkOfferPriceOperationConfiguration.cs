using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class BulkOfferPriceOperationConfiguration : IEntityTypeConfiguration<BulkOfferPriceOperation>
{
    public void Configure(EntityTypeBuilder<BulkOfferPriceOperation> builder)
    {
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(operation => operation.Actor).IsRequired().HasMaxLength(320);
        builder.Property(operation => operation.PrimaryEmail).IsRequired().HasColumnType("text");
        builder.Property(operation => operation.SnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.HasIndex(operation => operation.AppliedAtUtc);
    }
}
