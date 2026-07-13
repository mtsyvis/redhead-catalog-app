using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class SiteWebmasterOfferLinkbuilderMailboxConfiguration : IEntityTypeConfiguration<SiteWebmasterOfferLinkbuilderMailbox>
{
    public void Configure(EntityTypeBuilder<SiteWebmasterOfferLinkbuilderMailbox> builder)
    {
        builder.ToTable("SiteWebmasterOfferLinkbuilderMailboxes");

        builder.HasKey(link => new { link.SiteWebmasterOfferId, link.LinkbuilderMailboxId });

        builder.Property(link => link.Source)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(LinkbuilderMailboxOfferSource.Import)
            .HasSentinel((LinkbuilderMailboxOfferSource)0);

        builder.Property(link => link.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(link => link.SiteWebmasterOffer)
            .WithMany(offer => offer.LinkbuilderMailboxes)
            .HasForeignKey(link => link.SiteWebmasterOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.LinkbuilderMailbox)
            .WithMany(mailbox => mailbox.OfferLinks)
            .HasForeignKey(link => link.LinkbuilderMailboxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => link.LinkbuilderMailboxId);
    }
}
