using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> b)
    {
        b.ToTable("Partners");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CommissionPerCustomer).HasPrecision(18, 2);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ReferrerBusinessId).IsUnique().HasFilter("[ReferrerBusinessId] IS NOT NULL");
    }
}

public sealed class PartnerCommissionConfiguration : IEntityTypeConfiguration<PartnerCommission>
{
    public void Configure(EntityTypeBuilder<PartnerCommission> b)
    {
        b.ToTable("PartnerCommissions");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.PaidReference).HasMaxLength(100);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId);
        b.HasIndex(x => new { x.PartnerId, x.BusinessId }).IsUnique();   // one commission per referred business
    }
}
