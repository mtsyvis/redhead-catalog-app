using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class MultiSearchAnalyticsRequestConfiguration : IEntityTypeConfiguration<MultiSearchAnalyticsRequest>
{
    public void Configure(EntityTypeBuilder<MultiSearchAnalyticsRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.RequestId }).IsUnique();
        builder.HasIndex(x => new { x.SearchedAtUtc, x.Role });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MissingDomainSearchConfiguration : IEntityTypeConfiguration<MissingDomainSearch>
{
    public void Configure(EntityTypeBuilder<MissingDomainSearch> builder)
    {
        builder.HasKey(x => new { x.SearchId, x.Domain });
        builder.Property(x => x.Domain).HasMaxLength(253).IsRequired();
        builder.HasIndex(x => x.Domain);
        builder.HasOne(x => x.Search).WithMany(x => x.MissingDomains)
            .HasForeignKey(x => x.SearchId).OnDelete(DeleteBehavior.Cascade);
    }
}
