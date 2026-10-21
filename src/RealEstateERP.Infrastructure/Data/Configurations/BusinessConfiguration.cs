using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> b)
    {
        b.ToTable("Businesses");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.PhysicalAddress).HasMaxLength(500);
        b.Property(x => x.KraPin).HasMaxLength(20);
        b.Property(x => x.LogoPath).HasMaxLength(500);
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(x => x.PenaltyPercent).HasPrecision(5, 2);
        b.Property(x => x.PaymentInstructions).HasMaxLength(1000);
        b.Property(x => x.LastAutomationSummary).HasMaxLength(1000);
    }
}
