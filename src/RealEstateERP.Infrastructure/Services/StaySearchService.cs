using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// The guest site. No business is signed in here, so every read ignores the tenant filters and states its own rules
/// (published, not deleted), and every new row carries the stay's business explicitly.
/// </summary>
public sealed class StaySearchService(ApplicationDbContext db, TimeProvider clock, IFileStorage storage, IEmailService? email = null) : IStaySearchService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));   // East Africa Time
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Published stays a guest may see: on short-stay properties, with a nightly rate, nothing deleted.</summary>
    private IQueryable<StayUnit> Published => db.StayUnits.IgnoreQueryFilters()
        .Where(s => !s.IsDeleted && s.IsPublished && s.BaseNightly > 0
                    && !s.Unit.IsDeleted && s.Unit.Status != UnitStatus.Inactive
                    && !s.Unit.Asset.IsDeleted && s.Unit.Asset.Purpose == PropertyPurpose.ShortStay);

    private IQueryable<BookingNight> LiveNights => db.BookingNights.IgnoreQueryFilters().Where(n => !n.IsDeleted);

    public async Task<IReadOnlyList<PublicStayCard>> SearchAsync(string? where, DateOnly? checkIn, DateOnly? checkOut, int guests, CancellationToken ct = default)
    {
        var q = Published.Where(s => s.MaxGuests >= Math.Max(1, guests));
        if (!string.IsNullOrWhiteSpace(where))
        {
            var w = where.Trim();
            q = q.Where(s => s.Title.Contains(w) || s.Unit.Asset.Name.Contains(w)
                             || (s.Unit.Asset.Location != null && s.Unit.Asset.Location.Contains(w))
                             || (s.Unit.Asset.County != null && s.Unit.Asset.County.Contains(w)));
        }
        var hasDates = checkIn.HasValue && checkOut.HasValue && checkOut.Value > checkIn.Value;
        if (hasDates)
        {
            DateOnly from = checkIn!.Value, to = checkOut!.Value;
            var nights = LiveNights;
            q = q.Where(s => !nights.Any(n => n.UnitId == s.UnitId && n.Night >= from && n.Night < to));
        }

        var stays = await q.AsNoTracking().Include(s => s.Unit).ThenInclude(u => u.Asset).Include(s => s.Seasons).Include(s => s.Discounts)
            .OrderBy(s => s.BaseNightly).Take(60).ToListAsync(ct);
        var cards = new List<PublicStayCard>();
        foreach (var s in stays)
        {
            StayQuote? quote = null;
            if (hasDates)
            {
                quote = Quote(s, checkIn!.Value, checkOut!.Value);
                if (quote.Error is not null) continue;   // e.g. shorter than this stay's minimum: not offered for these dates
            }
            var (photoId, kind) = await FirstPhotoAsync(s, ct);
            cards.Add(new PublicStayCard(s.Id, s.Title, Place(s.Unit.Asset), s.Beds, s.MaxGuests, s.BaseNightly, photoId, kind, quote));
        }
        return cards;
    }

    public async Task<PublicStayDetails?> GetAsync(int stayUnitId, DateOnly? checkIn, DateOnly? checkOut, CancellationToken ct = default)
    {
        var s = await Published.AsNoTracking().Include(x => x.Unit).ThenInclude(u => u.Asset).Include(x => x.Seasons).Include(x => x.Discounts)
            .FirstOrDefaultAsync(x => x.Id == stayUnitId, ct);
        if (s is null) return null;
        var host = await db.Businesses.IgnoreQueryFilters().AsNoTracking().Where(b => b.Id == s.BusinessId).Select(b => b.Name).FirstAsync(ct);
        StayQuote? quote = null;
        if (checkIn.HasValue && checkOut.HasValue)
        {
            DateOnly from = checkIn.Value, to = checkOut.Value;
            quote = Quote(s, from, to);
            if (quote.Error is null && await LiveNights.AnyAsync(n => n.UnitId == s.UnitId && n.Night >= from && n.Night < to, ct))
                quote = StayQuote.Invalid("Some of these nights are already taken. Please choose other dates.");
        }
        var (photoId, kind) = await FirstPhotoAsync(s, ct);
        var unitPhotos = await db.UnitPhotos.IgnoreQueryFilters().AsNoTracking().Where(p => !p.IsDeleted && p.UnitId == s.UnitId)
            .OrderBy(p => p.SortOrder).Select(p => new PublicPhoto(p.Id, "unit", p.Caption)).ToListAsync(ct);
        var estatePhotos = await db.AssetPhotos.IgnoreQueryFilters().AsNoTracking().Where(p => !p.IsDeleted && p.AssetId == s.Unit.AssetId)
            .OrderBy(p => p.SortOrder).Select(p => new PublicPhoto(p.Id, "estate", p.Caption)).ToListAsync(ct);
        var horizon = Today.AddDays(180);
        var taken = await LiveNights.Where(n => n.UnitId == s.UnitId && n.Night >= Today && n.Night < horizon)
            .OrderBy(n => n.Night).Select(n => n.Night).ToListAsync(ct);
        var amenities = (s.Amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var discounts = s.Discounts.Where(d => !d.IsDeleted).OrderBy(d => d.MinNights).Select(d => $"{d.Percent}% off stays of {d.MinNights}+ nights").ToList();
        return new PublicStayDetails(new PublicStayCard(s.Id, s.Title, Place(s.Unit.Asset), s.Beds, s.MaxGuests, s.BaseNightly, photoId, kind, quote),
            host, s.Description, amenities, s.HouseRules, s.CheckInFrom, s.CheckOutBy, s.MinNights, s.MaxNights, s.CancellationPolicy,
            s.DepositPercent, s.CleaningFee, unitPhotos.Concat(estatePhotos).ToList(), taken, discounts);
    }

    public async Task<Result<string>> BookAsync(PublicBookingInput i, CancellationToken ct = default)
    {
        var s = await Published.Include(x => x.Unit).ThenInclude(u => u.Asset).Include(x => x.Seasons).Include(x => x.Discounts)
            .FirstOrDefaultAsync(x => x.Id == i.StayUnitId, ct);
        if (s is null) return Result<string>.Fail("This stay isn't available.");
        if (string.IsNullOrWhiteSpace(i.Name)) return Result<string>.Fail("Enter your name.");
        var phone = Phone(i.Phone);
        if (phone.Length < 9) return Result<string>.Fail("Enter your phone number, e.g. 0712 345 678.");
        if (i.Adults < 1) return Result<string>.Fail("At least one adult.");
        if (i.Adults + Math.Max(0, i.Children) > s.MaxGuests) return Result<string>.Fail($"This stay takes at most {s.MaxGuests} guests.");
        var quote = Quote(s, i.CheckIn, i.CheckOut);
        if (quote.Error is string error) return Result<string>.Fail(error);
        if (await LiveNights.AnyAsync(n => n.UnitId == s.UnitId && n.Night >= i.CheckIn && n.Night < i.CheckOut, ct))
            return Result<string>.Fail("Some of these nights have just been taken. Please choose other dates.");

        var businessId = s.BusinessId;
        var guest = await db.Guests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.BusinessId == businessId && !g.IsDeleted && g.Phone == phone, ct)
                    ?? db.Guests.Add(new Guest { BusinessId = businessId, FullName = i.Name.Trim(), Phone = phone }).Entity;
        if (!string.IsNullOrWhiteSpace(i.Email)) guest.Email = i.Email.Trim();
        var count = await db.Bookings.IgnoreQueryFilters().CountAsync(b => b.BusinessId == businessId, ct);
        var booking = new Booking
        {
            BusinessId = businessId, BookingNumber = $"BK-{count + 1:00000}", StayUnitId = s.Id, Guest = guest,
            CheckIn = i.CheckIn, CheckOut = i.CheckOut, Nights = quote.NightCount, Adults = i.Adults, Children = Math.Max(0, i.Children),
            Channel = BookingChannel.Direct, Status = BookingStatus.Held, HoldExpiresUtc = Now.Add(BookingRules.DepositHold),
            NightsTotal = quote.NightsTotal, CleaningFee = quote.CleaningFee, Discount = quote.Discount, Total = quote.Total, DepositDue = quote.Deposit,
            Notes = string.IsNullOrWhiteSpace(i.Message) ? null : $"Guest's message: {i.Message.Trim()}"
        };
        db.Bookings.Add(booking);
        foreach (var n in quote.Nights) db.BookingNights.Add(new BookingNight { BusinessId = businessId, UnitId = s.UnitId, Night = n.Night, Booking = booking });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();   // the double-booking guard refused: someone booked these nights a moment ago
            return Result<string>.Fail("Some of these nights have just been taken. Please choose other dates.");
        }

        var host = await db.Businesses.IgnoreQueryFilters().AsNoTracking().Where(b => b.Id == businessId)
            .Select(b => new { b.Name, b.Phone, b.Email, b.PaymentInstructions }).FirstAsync(ct);
        if (email is not null && guest.Email is string to)
            await TrySendAsync(new EmailMessage(to, $"Booking {booking.BookingNumber}: pay the deposit to confirm", Frame(host.Name, $"""
                <p>Dear {Html(guest.FullName)},</p>
                <p>Thank you for booking <strong>{Html(s.Title)}</strong> ({Html(Place(s.Unit.Asset))}), {i.CheckIn:ddd d MMM} to {i.CheckOut:ddd d MMM yyyy}.</p>
                <p><strong>To confirm, pay the deposit of KES {booking.DepositDue:N0} within 24 hours.</strong> Total for the stay: KES {booking.Total:N0}.</p>
                {HowToPay(host.PaymentInstructions, host.Phone, booking.BookingNumber)}
                <p>Your booking number: <strong>{booking.BookingNumber}</strong>. You can check it any time under "My booking" on booking.shambanyumba.com with this number and your phone number.</p>
                """, host.Phone), host.Email), ct);
        if (email is not null && host.Email is string hostEmail)
            await TrySendAsync(new EmailMessage(hostEmail, $"New booking request {booking.BookingNumber}: {s.Title}", Frame(host.Name, $"""
                <p><strong>{Html(guest.FullName)}</strong> ({phone}) booked <strong>{Html(s.Title)}</strong>, {i.CheckIn:ddd d MMM} to {i.CheckOut:ddd d MMM yyyy}, {quote.NightCount} night(s), KES {booking.Total:N0}.</p>
                <p>The nights are held for 24 hours while the guest pays the deposit of KES {booking.DepositDue:N0}. Record the payment on the booking when it arrives; it then confirms automatically.</p>
                """, null)), ct);
        return Result<string>.Ok(booking.BookingNumber);
    }

    public async Task<GuestBookingView?> FindAsync(string bookingNumber, string phone, CancellationToken ct = default)
    {
        var number = (bookingNumber ?? "").Trim().ToUpperInvariant();
        var digits = Phone(phone);
        if (number.Length == 0 || digits.Length < 9) return null;
        var b = await db.Bookings.IgnoreQueryFilters().AsNoTracking().Include(x => x.Guest).Include(x => x.StayUnit).ThenInclude(s => s.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.BookingNumber == number && x.Guest.Phone.EndsWith(digits.Substring(digits.Length - 9)), ct);
        if (b is null) return null;
        var host = await db.Businesses.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == b.BusinessId)
            .Select(x => new { x.Name, x.Phone, x.PaymentInstructions }).FirstAsync(ct);
        return new GuestBookingView(b.BookingNumber, b.Status, b.StayUnit.Title, Place(b.StayUnit.Unit.Asset), host.Name, host.Phone,
            b.CheckIn, b.CheckOut, b.Nights, b.StayUnit.CheckInFrom, b.StayUnit.CheckOutBy, b.Total, b.AmountPaid, b.DepositDue, b.HoldExpiresUtc, host.PaymentInstructions);
    }

    public async Task<(Stream Content, string ContentType)?> OpenPhotoAsync(string kind, int photoId, CancellationToken ct = default)
    {
        string? key = null, name = null;
        if (kind == "unit")
        {
            var p = await db.UnitPhotos.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == photoId && !x.IsDeleted, ct);
            if (p is not null && await Published.AnyAsync(s => s.UnitId == p.UnitId, ct)) (key, name) = (p.StorageKey, p.FileName);
        }
        else if (kind == "estate")
        {
            var p = await db.AssetPhotos.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == photoId && !x.IsDeleted, ct);
            if (p is not null && await Published.AnyAsync(s => s.Unit.AssetId == p.AssetId, ct)) (key, name) = (p.StorageKey, p.FileName);
        }
        if (key is null) return null;   // only photos of published stays are public
        var stream = await storage.OpenReadAsync(key, ct);
        if (stream is null) return null;
        var type = Path.GetExtension(name ?? "").ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg" };
        return (stream, type);
    }

    public async Task<int> ExpireHoldsAsync(CancellationToken ct = default)
    {
        var now = Now;
        var expired = await db.Bookings.IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Held && b.HoldExpiresUtc != null && b.HoldExpiresUtc < now).Take(200).ToListAsync(ct);
        foreach (var b in expired)
        {
            b.Status = BookingStatus.Cancelled;
            b.CancelledAtUtc = now;
            b.CancellationReason = "Deposit not received within 24 hours";
            var nights = await db.BookingNights.IgnoreQueryFilters().Where(n => n.BookingId == b.Id && !n.IsDeleted).ToListAsync(ct);
            db.BookingNights.RemoveRange(nights);
        }
        if (expired.Count > 0) await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    // ------------------------------------------------------------------ helpers

    private StayQuote Quote(StayUnit s, DateOnly checkIn, DateOnly checkOut)
        => StayPricing.Quote(s, s.Seasons.Where(x => !x.IsDeleted), s.Discounts.Where(x => !x.IsDeleted), checkIn, checkOut, Today);

    private async Task<(int?, string?)> FirstPhotoAsync(StayUnit s, CancellationToken ct)
    {
        var unit = await db.UnitPhotos.IgnoreQueryFilters().AsNoTracking().Where(p => !p.IsDeleted && p.UnitId == s.UnitId)
            .OrderBy(p => p.SortOrder).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (unit is not null) return (unit, "unit");
        var estate = await db.AssetPhotos.IgnoreQueryFilters().AsNoTracking().Where(p => !p.IsDeleted && p.AssetId == s.Unit.AssetId)
            .OrderBy(p => p.SortOrder).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return estate is not null ? (estate, "estate") : (null, null);
    }

    private static string Place(Asset a) => string.Join(", ", new[] { a.Location, a.County }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string Phone(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());
    private static string Html(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string HowToPay(string? instructions, string? phone, string bookingNumber)
        => string.IsNullOrWhiteSpace(instructions)
            ? $"<p>The host will contact you{(phone is null ? "" : $" from {Html(phone)}")} with how to pay.</p>"
            : $"<p><strong>How to pay:</strong><br/>{Html(instructions).Replace("\n", "<br/>")}<br/>Use <strong>{bookingNumber}</strong> as the account number or reference.</p>";

    private static string Frame(string hostName, string body, string? phone)
        => $"""
           <div style="font-family:Arial,sans-serif;font-size:14px;color:#0F1D2E;max-width:560px">
             <h2 style="color:#0B2A4A;margin:0 0 12px">{Html(hostName)}</h2>
             {body}
             <p style="color:#5B6B80;font-size:12px;margin-top:24px">{Html(hostName)}{(phone is null ? "" : $" · {Html(phone)}")} · Booked with ShambaNyumba Stays</p>
           </div>
           """;

    private async Task TrySendAsync(EmailMessage message, CancellationToken ct)
    {
        try { await email!.SendAsync(message, ct); } catch (Exception) { /* the booking stands even if an email fails */ }
    }
}
