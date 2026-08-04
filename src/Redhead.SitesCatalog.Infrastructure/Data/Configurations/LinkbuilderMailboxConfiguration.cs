using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class LinkbuilderMailboxConfiguration : IEntityTypeConfiguration<LinkbuilderMailbox>
{
    public void Configure(EntityTypeBuilder<LinkbuilderMailbox> builder)
    {
        builder.ToTable("LinkbuilderMailboxes");

        builder.HasKey(mailbox => mailbox.Id);

        builder.Property(mailbox => mailbox.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(mailbox => mailbox.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mailbox => mailbox.Aliases)
            .IsRequired()
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");

        builder.Property(mailbox => mailbox.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(mailbox => mailbox.CreatedAtUtc)
            .IsRequired();

        builder.Property(mailbox => mailbox.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(mailbox => mailbox.Email)
            .IsUnique();

        builder.HasIndex(mailbox => mailbox.Aliases)
            .HasMethod("gin");

        builder.HasIndex(mailbox => mailbox.IsActive);
    }
}
