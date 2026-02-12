using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class ImportLogConfiguration : IEntityTypeConfiguration<ImportLog>
{
    public void Configure(EntityTypeBuilder<ImportLog> builder)
    {
        builder.ToTable("ImportLogs");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(i => i.UserEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.TimestampUtc)
            .IsRequired();

        builder.HasIndex(i => i.TimestampUtc);

        builder.Property(i => i.Inserted)
            .IsRequired();

        builder.Property(i => i.Duplicates)
            .IsRequired();

        builder.Property(i => i.Matched)
            .IsRequired();

        builder.Property(i => i.Unmatched)
            .IsRequired();

        builder.Property(i => i.ErrorsCount)
            .IsRequired();
    }
}
