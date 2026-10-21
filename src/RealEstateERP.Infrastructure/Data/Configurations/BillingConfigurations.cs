using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class ChargeDefinitionConfiguration : IEntityTypeConfiguration<ChargeDefinition>
{
    public void Configure(EntityTypeBuilder<ChargeDefinition> b)
    {
        b.ToTable("ChargeDefinitions");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.UnitRate).HasPrecision(18, 4);
        b.Property(x => x.TaxPercent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.BusinessId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class LeaseChargeConfiguration : IEntityTypeConfiguration<LeaseCharge>
{
    public void Configure(EntityTypeBuilder<LeaseCharge> b)
    {
        b.ToTable("LeaseCharges");
        b.Property(x => x.UnitRateOverride).HasPrecision(18, 4);
        b.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);
        b.HasOne(x => x.ChargeDefinition).WithMany().HasForeignKey(x => x.ChargeDefinitionId);
        b.HasIndex(x => new { x.LeaseId, x.ChargeDefinitionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class UtilityMeterReadingConfiguration : IEntityTypeConfiguration<UtilityMeterReading>
{
    public void Configure(EntityTypeBuilder<UtilityMeterReading> b)
    {
        b.ToTable("UtilityMeterReadings");
        b.Property(x => x.PreviousReading).HasPrecision(18, 3);
        b.Property(x => x.CurrentReading).HasPrecision(18, 3);
        b.Property(x => x.Consumption).HasPrecision(18, 3);
        b.Property(x => x.UnitRate).HasPrecision(18, 4);
        b.Property(x => x.Notes).HasMaxLength(300);

        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
        b.HasOne(x => x.ChargeDefinition).WithMany().HasForeignKey(x => x.ChargeDefinitionId);
        b.HasOne(x => x.BilledInvoice).WithMany().HasForeignKey(x => x.BilledInvoiceId);

        b.HasIndex(x => new { x.UnitId, x.ChargeDefinitionId, x.ReadingDate }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.BusinessId, x.BilledInvoiceId });
    }
}

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        b.Property(x => x.InvoiceNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.StatusReason).HasMaxLength(500);
        b.Ignore(x => x.Balance);

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);
        b.HasOne(x => x.PenaltyForInvoice).WithMany().HasForeignKey(x => x.PenaltyForInvoiceId);

        // A late penalty is charged at most once per overdue invoice (a cancelled penalty doesn't count).
        b.HasIndex(x => x.PenaltyForInvoiceId).IsUnique()
            .HasFilter("[PenaltyForInvoiceId] IS NOT NULL AND [Status] <> 'Cancelled' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Invoices_OnePenaltyPerInvoice");
        b.HasIndex(x => new { x.BusinessId, x.InvoiceNumber }).IsUnique();
        b.HasIndex(x => new { x.BusinessId, x.Status, x.DueDate });
        // THE database rule: one monthly invoice per lease per month (cancelled ones don't count).
        b.HasIndex(x => new { x.LeaseId, x.PeriodStart }).IsUnique()
            .HasFilter("[Kind] = 'Monthly' AND [Status] <> 'Cancelled' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Invoices_OneMonthlyPerLeasePeriod");
    }
}

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("InvoiceLines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 4);
        b.Property(x => x.TaxPercent).HasPrecision(5, 2);
        b.HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId);
    }
}
