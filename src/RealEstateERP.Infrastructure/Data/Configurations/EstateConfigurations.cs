using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class PropertyChargeConfiguration : IEntityTypeConfiguration<PropertyCharge>
{
    public void Configure(EntityTypeBuilder<PropertyCharge> b)
    {
        b.ToTable("PropertyCharges");
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        b.HasOne(x => x.ChargeDefinition).WithMany().HasForeignKey(x => x.ChargeDefinitionId);
        // Each charge once per estate.
        b.HasIndex(x => new { x.AssetId, x.ChargeDefinitionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class AssetPhotoConfiguration : IEntityTypeConfiguration<AssetPhoto>
{
    public void Configure(EntityTypeBuilder<AssetPhoto> b)
    {
        b.ToTable("AssetPhotos");
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(150);
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
    }
}

public sealed class UnitPhotoConfiguration : IEntityTypeConfiguration<UnitPhoto>
{
    public void Configure(EntityTypeBuilder<UnitPhoto> b)
    {
        b.ToTable("UnitPhotos");
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(150);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
    }
}
