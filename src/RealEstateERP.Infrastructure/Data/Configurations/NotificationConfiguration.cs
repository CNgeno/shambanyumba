using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(500);
        b.Property(x => x.Url).HasMaxLength(300);
        b.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId);
        b.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
    }
}
