using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("Assets");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Location).HasMaxLength(300);
        b.Property(x => x.County).HasMaxLength(50);
        b.Property(x => x.TitleDeedNumber).HasMaxLength(100);
        b.Property(x => x.SellerName).HasMaxLength(150);
        b.Property(x => x.SubdivisionReference).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.LandSizeAcres).HasPrecision(18, 4);   // 0.0625 acre plots

        b.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class StructureNodeConfiguration : IEntityTypeConfiguration<StructureNode>
{
    public void Configure(EntityTypeBuilder<StructureNode> b)
    {
        b.ToTable("StructureNodes");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();

        b.HasOne(x => x.Asset).WithMany(x => x.Nodes).HasForeignKey(x => x.AssetId);
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);

        b.HasIndex(x => new { x.AssetId, x.ParentId });
    }
}

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("Units");
        b.Property(x => x.UnitNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.TitleNumber).HasMaxLength(100);

        b.HasOne(x => x.Asset).WithMany(x => x.Units).HasForeignKey(x => x.AssetId);
        b.HasOne(x => x.StructureNode).WithMany().HasForeignKey(x => x.StructureNodeId);

        // Unit numbers are unique within a property.
        b.HasIndex(x => new { x.AssetId, x.UnitNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}
