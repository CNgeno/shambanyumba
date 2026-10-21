using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.Property(x => x.ReceiptNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(50);
        b.Property(x => x.PayerName).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.ReversedBy).HasMaxLength(450);
        b.Property(x => x.ReversalReason).HasMaxLength(500);

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);

        b.HasIndex(x => new { x.BusinessId, x.ReceiptNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.TenantId, x.Status });
        b.HasIndex(x => new { x.BusinessId, x.PaymentDate });
        // The same M-PESA code / bank reference / cheque can't be recorded twice (unless the first was reversed).
        b.HasIndex(x => new { x.BusinessId, x.Method, x.Reference }).IsUnique()
            .HasFilter("[Reference] IS NOT NULL AND [Status] <> 'Reversed' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Payments_Reference");
    }
}

public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.ToTable("PaymentAllocations");
        b.HasOne(x => x.Payment).WithMany(x => x.Allocations).HasForeignKey(x => x.PaymentId);
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId);
        b.HasIndex(x => new { x.InvoiceId, x.IsReversed });
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.ToTable("Refunds");
        b.Property(x => x.RefundNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);
        b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId);
        b.HasIndex(x => new { x.BusinessId, x.RefundNumber }).IsUnique();
    }
}
