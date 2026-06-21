using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class AhrefsSyncRunItemConfiguration : IEntityTypeConfiguration<AhrefsSyncRunItem>
{
    public void Configure(EntityTypeBuilder<AhrefsSyncRunItem> builder)
    {
        builder.ToTable("AhrefsSyncRunItems", table =>
        {
            table.HasCheckConstraint(
                "CK_AhrefsSyncRunItems_Status_ValidValue",
                @"""Status"" IN (1, 2, 3, 4)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Domain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.SnapshotMonth).HasColumnType("date").IsRequired();
        builder.Property(item => item.ErrorMessage).HasColumnType("text");
        builder.HasIndex(item => item.RunId);
        builder.HasIndex(item => new { item.RunId, item.Domain });
    }
}
