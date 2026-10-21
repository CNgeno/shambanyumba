using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class ProjectCostConfiguration : IEntityTypeConfiguration<ProjectCost>
{
    public void Configure(EntityTypeBuilder<ProjectCost> b)
    {
        b.ToTable("ProjectCosts");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Supplier).HasMaxLength(150);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.ReceiptStorageKey).HasMaxLength(300);
        b.Property(x => x.ReceiptFileName).HasMaxLength(255);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
        b.HasIndex(x => new { x.AssetId, x.Kind, x.Category });
    }
}
