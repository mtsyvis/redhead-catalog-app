using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class WebmasterConfiguration : IEntityTypeConfiguration<Webmaster>
{
    public void Configure(EntityTypeBuilder<Webmaster> builder)
    {
        builder.ToTable("Webmasters");

        builder.HasKey(webmaster => webmaster.Id);

        builder.Property(webmaster => webmaster.DisplayName)
            .HasMaxLength(300);

        builder.Property(webmaster => webmaster.ContactRawText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(webmaster => webmaster.NormalizedContactRawText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(webmaster => webmaster.PrimaryEmail)
            .HasMaxLength(320);

        builder.Property(webmaster => webmaster.Notes)
            .HasColumnType("text");

        builder.Property(webmaster => webmaster.CreatedAtUtc)
            .IsRequired();

        builder.Property(webmaster => webmaster.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(webmaster => webmaster.NormalizedContactRawText)
            .IsUnique();

        builder.HasIndex(webmaster => webmaster.PrimaryEmail);
    }
}
