using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class LiteMultiSearchUsageConfiguration : IEntityTypeConfiguration<LiteMultiSearchUsage>
{
    public void Configure(EntityTypeBuilder<LiteMultiSearchUsage> builder)
    {
        builder.ToTable("LiteMultiSearchUsages", table =>
        {
            table.HasCheckConstraint(
                "CK_LiteMultiSearchUsages_DomainsUsed_NonNegative",
                @"""DomainsUsed"" >= 0");
        });

        builder.HasKey(usage => new { usage.UserId, usage.MonthStartUtc });

        builder.Property(usage => usage.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(usage => usage.MonthStartUtc)
            .IsRequired();

        builder.Property(usage => usage.DomainsUsed)
            .IsRequired();

        builder.Property(usage => usage.CreatedAtUtc)
            .IsRequired();

        builder.Property(usage => usage.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(usage => usage.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
