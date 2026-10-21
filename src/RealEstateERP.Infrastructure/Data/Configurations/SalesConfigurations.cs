using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class BuyerConfiguration : IEntityTypeConfiguration<Buyer>
{
    public void Configure(EntityTypeBuilder<Buyer> b)
    {
        b.ToTable("Buyers");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(50);
        b.Property(x => x.KraPin).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.PostalAddress).HasMaxLength(300);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.BusinessId, x.FullName });
    }
}

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales");
        b.Property(x => x.SaleNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Ignore(x => x.Balance);
        b.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
        b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId);
        b.HasIndex(x => new { x.BusinessId, x.SaleNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status });
        // THE database rule: one live sale per unit (cancelled sales don't count).
        b.HasIndex(x => x.UnitId).IsUnique().HasFilter("[Status] <> 'Cancelled' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Sales_OneLivePerUnit");
    }
}

public sealed class SaleInstalmentConfiguration : IEntityTypeConfiguration<SaleInstalment>
{
    public void Configure(EntityTypeBuilder<SaleInstalment> b)
    {
        b.ToTable("SaleInstalments");
        b.Property(x => x.Description).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Sale).WithMany(x => x.Instalments).HasForeignKey(x => x.SaleId);
        b.HasIndex(x => new { x.SaleId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.DueDate });
    }
}

public sealed class SaleMilestoneConfiguration : IEntityTypeConfiguration<SaleMilestone>
{
    public void Configure(EntityTypeBuilder<SaleMilestone> b)
    {
        b.ToTable("SaleMilestones");
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.DocumentStorageKey).HasMaxLength(300);
        b.Property(x => x.DocumentFileName).HasMaxLength(255);
        b.HasOne(x => x.Sale).WithMany(x => x.Milestones).HasForeignKey(x => x.SaleId);
        b.HasIndex(x => new { x.SaleId, x.Type }).IsUnique();
    }
}

public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> b)
    {
        b.ToTable("SalePayments");
        b.Property(x => x.ReceiptNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.ReversedBy).HasMaxLength(450);
        b.Property(x => x.ReversalReason).HasMaxLength(500);
        b.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId);
        b.HasIndex(x => new { x.BusinessId, x.ReceiptNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.PaymentDate });
        b.HasIndex(x => new { x.BusinessId, x.Method, x.Reference }).IsUnique()
            .HasFilter("[Reference] IS NOT NULL AND [Status] <> 'Reversed' AND [IsDeleted] = 0");
    }
}

public sealed class SaleAllocationConfiguration : IEntityTypeConfiguration<SaleAllocation>
{
    public void Configure(EntityTypeBuilder<SaleAllocation> b)
    {
        b.ToTable("SaleAllocations");
        b.HasOne(x => x.SalePayment).WithMany(x => x.Allocations).HasForeignKey(x => x.SalePaymentId);
        b.HasOne(x => x.Instalment).WithMany().HasForeignKey(x => x.InstalmentId);
    }
}
