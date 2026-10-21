using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> b)
    {
        b.ToTable("Listings");
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId);
        // One open listing per unit and type (closed ones don't count).
        b.HasIndex(x => new { x.UnitId, x.Type }).IsUnique().HasFilter("[Status] <> 'Closed' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Listings_OneOpenPerUnitType");
        b.HasIndex(x => new { x.Status, x.Type, x.PublishedAtUtc });
    }
}

public sealed class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> b)
    {
        b.ToTable("ListingPhotos");
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.HasOne(x => x.Listing).WithMany(x => x.Photos).HasForeignKey(x => x.ListingId);
    }
}

public sealed class ListingLeadConfiguration : IEntityTypeConfiguration<ListingLead>
{
    public void Configure(EntityTypeBuilder<ListingLead> b)
    {
        b.ToTable("ListingLeads");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.VerificationTokenHash).HasMaxLength(64);
        b.Property(x => x.RefundReason).HasMaxLength(300);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId);
        b.HasIndex(x => x.VerificationTokenHash).IsUnique().HasFilter("[VerificationTokenHash] IS NOT NULL");
        b.HasIndex(x => new { x.BusinessId, x.Status, x.CreatedAtUtc });
        b.HasIndex(x => new { x.ListingId, x.Email });
        b.HasIndex(x => new { x.ListingId, x.Phone });
    }
}
