using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class CurrencyReferenceConfiguration : IEntityTypeConfiguration<CurrencyReferenceSnapshot>,
    IEntityTypeConfiguration<CurrencyReferenceRefreshState>
{
    public void Configure(EntityTypeBuilder<CurrencyReferenceSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.UsdRatesJson).HasColumnType("jsonb");
    }

    public void Configure(EntityTypeBuilder<CurrencyReferenceRefreshState> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.ToTable("CurrencyReferenceRefreshStates", t => t.HasCheckConstraint("CK_CurrencyRefresh_Singleton", "\"Id\" = 1"));
    }
}
