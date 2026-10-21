using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class BusinessInvitationConfiguration : IEntityTypeConfiguration<BusinessInvitation>
{
    public void Configure(EntityTypeBuilder<BusinessInvitation> b)
    {
        b.ToTable("BusinessInvitations");
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.AcceptedByUserId).HasMaxLength(450);
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.Email, x.AcceptedAtUtc, x.RevokedAtUtc });
        b.HasIndex(x => new { x.BusinessId, x.AcceptedAtUtc });
    }
}
