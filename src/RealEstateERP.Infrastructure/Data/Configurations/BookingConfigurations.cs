using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Infrastructure.Data.Configurations;

/// <summary>booking.shambanyumba.com tables live in the "booking" schema of the same database.</summary>
public sealed class StayUnitConfiguration : IEntityTypeConfiguration<StayUnit>
{
    public void Configure(EntityTypeBuilder<StayUnit> b)
    {
        b.ToTable("StayUnits", "booking");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Amenities).HasMaxLength(1000);
        b.Property(x => x.HouseRules).HasMaxLength(2000);
        b.Property(x => x.BaseNightly).HasPrecision(18, 2);
        b.Property(x => x.WeekendNightly).HasPrecision(18, 2);
        b.Property(x => x.CleaningFee).HasPrecision(18, 2);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.UnitId).IsUnique().HasFilter("[IsDeleted] = 0");   // one short stay per unit
    }
}

public sealed class StaySeasonConfiguration : IEntityTypeConfiguration<StaySeason>
{
    public void Configure(EntityTypeBuilder<StaySeason> b)
    {
        b.ToTable("StaySeasons", "booking");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Nightly).HasPrecision(18, 2);
        b.HasOne(x => x.StayUnit).WithMany(s => s.Seasons).HasForeignKey(x => x.StayUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("Guests", "booking");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.HasIndex(x => new { x.BusinessId, x.Phone });
    }
}

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("Bookings", "booking");
        b.Property(x => x.BookingNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.ExternalRef).HasMaxLength(100);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);
        foreach (var p in new[] { nameof(Booking.NightsTotal), nameof(Booking.CleaningFee), nameof(Booking.Discount), nameof(Booking.Total), nameof(Booking.DepositDue), nameof(Booking.AmountPaid) })
            b.Property(p).HasPrecision(18, 2);
        b.Ignore(x => x.Balance);
        b.HasOne(x => x.StayUnit).WithMany().HasForeignKey(x => x.StayUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Guest).WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BusinessId, x.BookingNumber }).IsUnique();
        b.HasIndex(x => new { x.StayUnitId, x.CheckIn });
    }
}

public sealed class BookingNightConfiguration : IEntityTypeConfiguration<BookingNight>
{
    public void Configure(EntityTypeBuilder<BookingNight> b)
    {
        b.ToTable("BookingNights", "booking");
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CalendarBlock).WithMany().HasForeignKey(x => x.CalendarBlockId).OnDelete(DeleteBehavior.Restrict);
        // The double-booking guard: one live row per unit per night.
        b.HasIndex(x => new { x.UnitId, x.Night }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class CalendarBlockConfiguration : IEntityTypeConfiguration<CalendarBlock>
{
    public void Configure(EntityTypeBuilder<CalendarBlock> b)
    {
        b.ToTable("CalendarBlocks", "booking");
        b.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.StayUnit).WithMany().HasForeignKey(x => x.StayUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CleaningTaskConfiguration : IEntityTypeConfiguration<CleaningTask>
{
    public void Configure(EntityTypeBuilder<CleaningTask> b)
    {
        b.ToTable("CleaningTasks", "booking");
        b.Property(x => x.DoneBy).HasMaxLength(256);
        b.HasOne(x => x.StayUnit).WithMany().HasForeignKey(x => x.StayUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StayDiscountConfiguration : IEntityTypeConfiguration<StayDiscount>
{
    public void Configure(EntityTypeBuilder<StayDiscount> b)
    {
        b.ToTable("StayDiscounts", "booking");
        b.HasOne(x => x.StayUnit).WithMany(s => s.Discounts).HasForeignKey(x => x.StayUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BookingPaymentConfiguration : IEntityTypeConfiguration<BookingPayment>
{
    public void Configure(EntityTypeBuilder<BookingPayment> b)
    {
        b.ToTable("BookingPayments", "booking");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(50);
        b.Property(x => x.ReceiptNumber).HasMaxLength(30);
        b.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        // A payment reference (M-PESA code) can be recorded only once per business.
        b.HasIndex(x => new { x.BusinessId, x.Reference }).IsUnique().HasFilter("[Reference] IS NOT NULL AND [IsDeleted] = 0");
    }
}
