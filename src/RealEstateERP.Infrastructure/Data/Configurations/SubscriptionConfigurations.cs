using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("SubscriptionPlans");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("Subscriptions");
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
        b.HasOne(x => x.SourcePayment).WithMany().HasForeignKey(x => x.SourcePaymentId);

        b.HasIndex(x => new { x.BusinessId, x.PeriodEndUtc });
        // A payment can create at most one period, enforced by the database.
        b.HasIndex(x => x.SourcePaymentId).IsUnique().HasFilter("[SourcePaymentId] IS NOT NULL");
    }
}

public sealed class SubscriptionPaymentConfiguration : IEntityTypeConfiguration<SubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPayment> b)
    {
        b.ToTable("SubscriptionPayments");
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);

        b.Property(x => x.Reference).HasMaxLength(50).IsRequired();
        b.Property(x => x.ProofStorageKey).HasMaxLength(300);
        b.Property(x => x.ProofFileName).HasMaxLength(255);
        b.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        b.Property(x => x.ReviewNote).HasMaxLength(500);

        b.HasIndex(x => new { x.BusinessId, x.Status });
        // The same M-PESA/bank reference can't be used twice (unless the earlier one was rejected).
        b.HasIndex(x => new { x.Method, x.Reference }).IsUnique()
            .HasFilter("[Status] <> 'Rejected' AND [IsDeleted] = 0");
    }
}

public sealed class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> b)
    {
        b.ToTable("SystemConfigurations");
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Group).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
