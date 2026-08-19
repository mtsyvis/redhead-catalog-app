using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class SiteWebmasterOfferConfiguration : IEntityTypeConfiguration<SiteWebmasterOffer>
{
    public void Configure(EntityTypeBuilder<SiteWebmasterOffer> builder)
    {
        builder.ToTable("SiteWebmasterOffers", table =>
        {
            table.HasCheckConstraint(
                "CK_SiteWebmasterOffers_Term_Consistency",
                "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR " +
                "(\"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR " +
                "(\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
        });

        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.SiteDomain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);

        builder.Property(offer => offer.ImportFingerprint)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(offer => offer.ContactRawText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(offer => offer.OutreachSenderRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.LinkbuilderMailboxRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.LinkPolicyText)
            .HasColumnType("text");

        builder.Property(offer => offer.DfLinksRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.SponsoredTagRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.CommentText)
            .HasColumnType("text");

        builder.Property(offer => offer.ClientRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.TermRawText)
            .HasColumnType("text");

        builder.Property(offer => offer.TermType)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(offer => offer.TermUnit)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(offer => offer.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(SiteWebmasterOfferStatus.Active)
            .HasSentinel((SiteWebmasterOfferStatus)0);

        builder.Property(offer => offer.CreatedAtUtc)
            .IsRequired();

        builder.Property(offer => offer.UpdatedAtUtc)
            .IsRequired();

        builder.Property(offer => offer.UpdatedBy)
            .HasMaxLength(320);

        builder.HasOne(offer => offer.Site)
            .WithMany(site => site.WebmasterOffers)
            .HasForeignKey(offer => offer.SiteDomain)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(offer => offer.Webmaster)
            .WithMany(webmaster => webmaster.Offers)
            .HasForeignKey(offer => offer.WebmasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(offer => offer.SiteDomain);
        builder.HasIndex(offer => offer.ImportFingerprint).IsUnique();
        builder.HasIndex(offer => new { offer.SiteDomain, offer.Status });
        builder.HasIndex(offer => offer.WebmasterId);
        builder.HasIndex(offer => offer.Status);
    }
}
