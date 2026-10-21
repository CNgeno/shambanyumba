using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class CollectionNoteConfiguration : IEntityTypeConfiguration<CollectionNote>
{
    public void Configure(EntityTypeBuilder<CollectionNote> b)
    {
        b.ToTable("CollectionNotes");
        b.Property(x => x.Note).HasMaxLength(1000).IsRequired();
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId);
        b.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
    }
}

public sealed class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> b)
    {
        b.ToTable("CreditTransactions");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(50);
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
    }
}

public sealed class CreditPurchaseConfiguration : IEntityTypeConfiguration<CreditPurchase>
{
    public void Configure(EntityTypeBuilder<CreditPurchase> b)
    {
        b.ToTable("CreditPurchases");
        b.Property(x => x.Reference).HasMaxLength(50).IsRequired();
        b.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasIndex(x => new { x.Method, x.Reference }).IsUnique().HasFilter("[Status] <> 'Rejected' AND [IsDeleted] = 0");
        b.HasIndex(x => new { x.BusinessId, x.Status });
    }
}
