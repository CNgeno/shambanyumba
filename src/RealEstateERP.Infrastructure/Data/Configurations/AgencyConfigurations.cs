using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class PropertyOwnerConfiguration : IEntityTypeConfiguration<PropertyOwner>
{
    public void Configure(EntityTypeBuilder<PropertyOwner> b)
    {
        b.ToTable("PropertyOwners");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(50);
        b.Property(x => x.KraPin).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.PostalAddress).HasMaxLength(300);
        b.Property(x => x.BankName).HasMaxLength(100);
        b.Property(x => x.BankAccountName).HasMaxLength(150);
        b.Property(x => x.BankAccountNumber).HasMaxLength(50);
        b.Property(x => x.MpesaNumber).HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.BusinessId, x.FullName });
    }
}

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("Agents");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.LicenseNumber).HasMaxLength(50);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.CommissionSharePercent).HasPrecision(5, 2);
        b.Property(x => x.UserId).HasMaxLength(450);
        // One agent profile per login within a business.
        b.HasIndex(x => new { x.BusinessId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public sealed class ManagementContractConfiguration : IEntityTypeConfiguration<ManagementContract>
{
    public void Configure(EntityTypeBuilder<ManagementContract> b)
    {
        b.ToTable("ManagementContracts");
        b.Property(x => x.ContractNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId);
        b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId);
        b.HasIndex(x => new { x.BusinessId, x.ContractNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}

public sealed class ContractPropertyConfiguration : IEntityTypeConfiguration<ContractProperty>
{
    public void Configure(EntityTypeBuilder<ContractProperty> b)
    {
        b.ToTable("ContractProperties");
        b.HasOne(x => x.Contract).WithMany(x => x.Properties).HasForeignKey(x => x.ContractId);
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        // THE database rule: a property is under at most one active management contract.
        b.HasIndex(x => x.AssetId).IsUnique().HasFilter("[IsActive] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_ContractProperties_OneActivePerAsset");
    }
}

public sealed class OwnerRemittanceConfiguration : IEntityTypeConfiguration<OwnerRemittance>
{
    public void Configure(EntityTypeBuilder<OwnerRemittance> b)
    {
        b.ToTable("OwnerRemittances");
        b.Property(x => x.RemittanceNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.PaidReference).HasMaxLength(50);
        b.Property(x => x.StatusReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId);
        b.HasIndex(x => new { x.BusinessId, x.RemittanceNumber }).IsUnique();
        // One remittance per contract per month (a cancelled one doesn't count).
        b.HasIndex(x => new { x.ContractId, x.PeriodStart }).IsUnique()
            .HasFilter("[Status] <> 'Cancelled' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_OwnerRemittances_OnePerContractMonth");
    }
}

public sealed class RemittanceLineConfiguration : IEntityTypeConfiguration<RemittanceLine>
{
    public void Configure(EntityTypeBuilder<RemittanceLine> b)
    {
        b.ToTable("RemittanceLines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Remittance).WithMany(x => x.Lines).HasForeignKey(x => x.RemittanceId);
    }
}
