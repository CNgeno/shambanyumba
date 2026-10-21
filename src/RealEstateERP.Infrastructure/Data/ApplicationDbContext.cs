using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser,
        TimeProvider clock)
        : this((DbContextOptions)options, currentUser, clock) { }

    /// <summary>For derived contexts (used by the test project).</summary>
    protected ApplicationDbContext(DbContextOptions options, ICurrentUserService currentUser, TimeProvider clock)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<BusinessInvitation> BusinessInvitations => Set<BusinessInvitation>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDocument> TenantDocuments => Set<TenantDocument>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<ChargeDefinition> ChargeDefinitions => Set<ChargeDefinition>();
    public DbSet<LeaseCharge> LeaseCharges => Set<LeaseCharge>();
    public DbSet<UtilityMeterReading> UtilityMeterReadings => Set<UtilityMeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<CollectionNote> CollectionNotes => Set<CollectionNote>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<CreditPurchase> CreditPurchases => Set<CreditPurchase>();
    public DbSet<PropertyOwner> PropertyOwners => Set<PropertyOwner>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<ManagementContract> ManagementContracts => Set<ManagementContract>();
    public DbSet<ContractProperty> ContractProperties => Set<ContractProperty>();
    public DbSet<OwnerRemittance> OwnerRemittances => Set<OwnerRemittance>();
    public DbSet<RemittanceLine> RemittanceLines => Set<RemittanceLine>();
    public DbSet<ProjectCost> ProjectCosts => Set<ProjectCost>();
    public DbSet<PropertyCharge> PropertyCharges => Set<PropertyCharge>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<PartnerCommission> PartnerCommissions => Set<PartnerCommission>();
    public DbSet<AssetPhoto> AssetPhotos => Set<AssetPhoto>();
    public DbSet<UnitPhoto> UnitPhotos => Set<UnitPhoto>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();
    public DbSet<ListingLead> ListingLeads => Set<ListingLead>();
    public DbSet<Buyer> Buyers => Set<Buyer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleInstalment> SaleInstalments => Set<SaleInstalment>();
    public DbSet<SaleMilestone> SaleMilestones => Set<SaleMilestone>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<SaleAllocation> SaleAllocations => Set<SaleAllocation>();

    /// <summary>Read by the tenant query filter on every query (EF parameterises it per execution).</summary>
    public int? CurrentBusinessId => _currentUser.BusinessId;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // NFR-02: money is always decimal(18,2).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        // All enums stored as readable strings.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            var clr = entityType.ClrType;

            // Only touch our domain entities; leave Identity tables as Microsoft configured them.
            if (!typeof(BaseEntity).IsAssignableFrom(clr))
                continue;

            if (typeof(AuditableEntity).IsAssignableFrom(clr))
                builder.Entity(clr).Property(nameof(AuditableEntity.RowVersion)).IsRowVersion();

            if (typeof(ISoftDelete).IsAssignableFrom(clr))
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clr).Invoke(this, [builder]);

            if (typeof(ITenantEntity).IsAssignableFrom(clr))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(clr).Invoke(this, [builder]);

                // Every tenant row references a real business at the database level too.
                builder.Entity(clr).HasOne(typeof(Business)).WithMany().HasForeignKey(nameof(ITenantEntity.BusinessId));
            }

            // RULE: no cascade deletes anywhere in the domain. Nothing is hard-deleted (NFR-03),
            // and this removes the "may cause cycles or multiple cascade paths" migration error for good.
            foreach (var fk in entityType.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static readonly MethodInfo ApplySoftDeleteFilterMethod =
        typeof(ApplicationDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(ApplicationDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : class, ISoftDelete
        => builder.Entity<TEntity>().HasQueryFilter(QueryFilters.SoftDelete, e => !e.IsDeleted);

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
        => builder.Entity<TEntity>().HasQueryFilter(QueryFilters.Tenant, e => e.BusinessId == CurrentBusinessId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyWriteRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyWriteRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Every write passes through here: tenant stamping + cross-tenant guard,
    /// soft-delete conversion, audit stamps. Services never set these fields themselves.
    /// </summary>
    private void ApplyWriteRules()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = _currentUser.UserId;
        var businessId = _currentUser.BusinessId;

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            // 1. Tenant rules
            if (entry.Entity is ITenantEntity tenant)
            {
                switch (entry.State)
                {
                    case EntityState.Added when tenant.BusinessId == 0:
                        tenant.BusinessId = businessId
                            ?? throw new InvalidOperationException(
                                $"Cannot create {entry.Metadata.ClrType.Name}: no business is selected.");
                        break;

                    case EntityState.Added when businessId.HasValue && tenant.BusinessId != businessId:
                        throw new InvalidOperationException(
                            $"Cross-tenant write blocked: {entry.Metadata.ClrType.Name} belongs to another business.");

                    case EntityState.Modified or EntityState.Deleted:
                        if (businessId.HasValue && tenant.BusinessId != businessId)
                            throw new InvalidOperationException(
                                $"Cross-tenant write blocked: {entry.Metadata.ClrType.Name} belongs to another business.");
                        if (entry.Property(nameof(ITenantEntity.BusinessId)).IsModified)
                            throw new InvalidOperationException("BusinessId cannot be changed after creation.");
                        break;
                }
            }

            // 2. Soft delete (NFR-03): Remove() becomes an UPDATE.
            if (entry.Entity is ISoftDelete soft && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAtUtc = now;
                soft.DeletedBy = userId;
            }

            // 3. Audit stamps
            if (entry.Entity is IAuditable audit)
            {
                if (entry.State == EntityState.Added)
                {
                    audit.CreatedAtUtc = now;
                    audit.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    audit.UpdatedAtUtc = now;
                    audit.UpdatedBy = userId;
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                }
            }
        }
    }
}
