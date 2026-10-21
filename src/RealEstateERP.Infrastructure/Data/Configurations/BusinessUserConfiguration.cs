using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class BusinessUserConfiguration : IEntityTypeConfiguration<BusinessUser>
{
    public void Configure(EntityTypeBuilder<BusinessUser> b)
    {
        b.ToTable("BusinessUsers");
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();

        b.HasOne(x => x.Business).WithMany(x => x.Members).HasForeignKey(x => x.BusinessId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);

        // One live membership per user per business.
        b.HasIndex(x => new { x.BusinessId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.UserId);
    }
}
