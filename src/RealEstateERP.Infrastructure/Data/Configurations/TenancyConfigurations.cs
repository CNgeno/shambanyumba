using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(50);
        b.Property(x => x.KraPin).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.AltPhone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.PostalAddress).HasMaxLength(300);
        b.Property(x => x.Employer).HasMaxLength(150);
        b.Property(x => x.EmergencyContactName).HasMaxLength(150);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
        b.Property(x => x.EmergencyContactRelationship).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(1000);

        // One tenant record per ID number within a business.
        b.HasIndex(x => new { x.BusinessId, x.IdNumber }).IsUnique()
            .HasFilter("[IdNumber] IS NOT NULL AND [IsDeleted] = 0");
        b.HasIndex(x => new { x.BusinessId, x.FullName });
        b.HasIndex(x => new { x.BusinessId, x.Phone });
    }
}

public sealed class TenantDocumentConfiguration : IEntityTypeConfiguration<TenantDocument>
{
    public void Configure(EntityTypeBuilder<TenantDocument> b)
    {
        b.ToTable("TenantDocuments");
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Tenant).WithMany(x => x.Documents).HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);
    }
}

public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> b)
    {
        b.ToTable("Leases");
        b.Property(x => x.LeaseNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.TerminationReason).HasMaxLength(500);
        b.Property(x => x.DepositNote).HasMaxLength(500);
        b.Ignore(x => x.DepositRefundDue);

        b.HasOne(x => x.Tenant).WithMany(x => x.Leases).HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
        b.HasOne(x => x.RenewedFrom).WithMany().HasForeignKey(x => x.RenewedFromLeaseId);

        b.HasIndex(x => new { x.BusinessId, x.LeaseNumber }).IsUnique();
        // THE database rule: a unit can have only one active lease.
        b.HasIndex(x => x.UnitId).IsUnique().HasFilter("[Status] = 'Active' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Leases_OneActivePerUnit");
        b.HasIndex(x => new { x.BusinessId, x.Status, x.EndDate });
        b.HasIndex(x => x.RenewedFromLeaseId).IsUnique().HasFilter("[RenewedFromLeaseId] IS NOT NULL");
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("NumberSequences");
        b.Property(x => x.Key).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.BusinessId, x.Key }).IsUnique();
    }
}
