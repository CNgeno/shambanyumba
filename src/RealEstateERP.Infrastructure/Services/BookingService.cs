using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Short-stay bookings. Availability is guarded twice: a check before saving (friendly message), and the unique index on
/// BookingNights (UnitId, Night) in the database, which refuses a second booking even when two arrive at the same moment.
/// </summary>
public sealed class BookingService(ApplicationDbContext db, TimeProvider clock, ICurrentUserService currentUser, IEmailService? email = null) : IBookingService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));   // East Africa Time
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ views

    public async Task<HostCalendar> CalendarAsync(DateOnly from, int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 62);
        var to = from.AddDays(days);
        var stays = await db.StayUnits.AsNoTracking().Include(s => s.Unit).OrderBy(s => s.Title).ToListAsync(ct);
        var unitIds = stays.Select(s => s.UnitId).ToList();
        var nights = await db.BookingNights.AsNoTracking()
            .Where(n => unitIds.Contains(n.UnitId) && n.Night >= from && n.Night < to)
            .Select(n => new
            {
                n.UnitId, n.Night, n.BookingId, n.CalendarBlockId,
                Number = n.Booking != null ? n.Booking.BookingNumber : null,
                Guest = n.Booking != null ? n.Booking.Guest.FullName : null,
                Status = n.Booking != null ? (BookingStatus?)n.Booking.Status : null,
                Channel = n.Booking != null ? (BookingChannel?)n.Booking.Channel : null,
                CheckIn = n.Booking != null ? (DateOnly?)n.Booking.CheckIn : null,
                Reason = n.CalendarBlock != null ? n.CalendarBlock.Reason : null
            }).ToListAsync(ct);
        var byKey = nights.ToDictionary(n => (n.UnitId, n.Night));
        var rows = stays.Select(s => new CalendarRow(s.Id, s.Title, s.Unit.UnitNumber,
            Enumerable.Range(0, days).Select(i => from.AddDays(i)).Select(d => byKey.TryGetValue((s.UnitId, d), out var n)
                ? new CalendarCell(d, n.BookingId, n.Number, n.Guest, n.Status, n.Channel, n.CheckIn == d || d == from, n.CalendarBlockId, n.Reason)
                : new CalendarCell(d, null, null, null, null, null, false, null, null)).ToList())).ToList();
        return new HostCalendar(from, days, rows);
    }

    public async Task<HostToday> TodayAsync(CancellationToken ct = default)
    {
        var today = Today;
        var arrivals = await Items(db.Bookings.Where(b => b.CheckIn == today && b.Status == BookingStatus.Confirmed), ct);
        var departures = await Items(db.Bookings.Where(b => b.CheckOut == today && b.Status == BookingStatus.CheckedIn), ct);
        var inHouse = await Items(db.Bookings.Where(b => b.Status == BookingStatus.CheckedIn), ct);
        var awaiting = await Items(db.Bookings.Where(b => b.Status == BookingStatus.Held), ct);
        return new HostToday(today, arrivals, departures, inHouse, await CleaningAsync(false, ct), awaiting);
    }

    public async Task<IReadOnlyList<BookingListItem>> ListAsync(string view, CancellationToken ct = default)
    {
        var today = Today;
        var q = view switch
        {
            "inhouse" => db.Bookings.Where(b => b.Status == BookingStatus.CheckedIn),
            "past" => db.Bookings.Where(b => b.Status == BookingStatus.CheckedOut),
            "cancelled" => db.Bookings.Where(b => b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.NoShow),
            _ => db.Bookings.Where(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Held) && b.CheckOut >= today)
        };
        var items = await Items(q, ct);
        return view is "past" or "cancelled" ? items.OrderByDescending(b => b.CheckIn).ToList() : items;
    }

    public Task<Booking?> GetAsync(int bookingId, CancellationToken ct = default)
        => db.Bookings.Include(b => b.Guest).Include(b => b.Payments.OrderBy(p => p.ReceivedOn))
            .Include(b => b.StayUnit).ThenInclude(s => s.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

    // ------------------------------------------------------------------ bookings

    public async Task<Result<int>> CreateAsync(NewBookingInput i, CancellationToken ct = default)
    {
        var stay = await db.StayUnits.Include(s => s.Seasons).Include(s => s.Discounts).FirstOrDefaultAsync(s => s.Id == i.StayUnitId, ct);
        if (stay is null) return Result<int>.Fail("Short stay not found.");
        if (string.IsNullOrWhiteSpace(i.GuestName)) return Result<int>.Fail("Enter the guest's name.");
        var phone = new string((i.GuestPhone ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (phone.Length < 9) return Result<int>.Fail("Enter the guest's phone number.");
        if (i.Adults < 1) return Result<int>.Fail("At least one adult.");
        if (i.Adults + Math.Max(0, i.Children) > stay.MaxGuests) return Result<int>.Fail($"This stay takes at most {stay.MaxGuests} guests.");

        var quote = StayPricing.Quote(stay, stay.Seasons, i.CheckIn, i.CheckOut, Today);
        if (quote.Error is string error) return Result<int>.Fail(error);
        if (await TakenAsync(stay.UnitId, i.CheckIn, i.CheckOut, ct) is string taken) return Result<int>.Fail(taken);

        var guest = await db.Guests.FirstOrDefaultAsync(g => g.Phone == phone, ct)
                    ?? db.Guests.Add(new Guest { FullName = i.GuestName.Trim(), Phone = phone }).Entity;
        if (!string.IsNullOrWhiteSpace(i.GuestEmail)) guest.Email = i.GuestEmail.Trim();

        var booking = new Booking
        {
            BookingNumber = await NextNumberAsync(ct), StayUnitId = stay.Id, Guest = guest,
            CheckIn = i.CheckIn, CheckOut = i.CheckOut, Nights = quote.NightCount, Adults = i.Adults, Children = Math.Max(0, i.Children),
            Channel = i.Channel, ExternalRef = string.IsNullOrWhiteSpace(i.ExternalRef) ? null : i.ExternalRef.Trim(),
            Status = BookingStatus.Confirmed, NightsTotal = quote.NightsTotal, CleaningFee = quote.CleaningFee, Discount = quote.Discount,
            Total = quote.Total, DepositDue = quote.Deposit, Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim()
        };
        db.Bookings.Add(booking);
        foreach (var n in quote.Nights) db.BookingNights.Add(new BookingNight { UnitId = stay.UnitId, Night = n.Night, Booking = booking });

        if (await SaveGuardedAsync(ct) is string clash) return Result<int>.Fail(clash);
        return Result<int>.Ok(booking.Id);
    }

    public async Task<Result> CheckInAsync(int bookingId, CancellationToken ct = default)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (b is null) return Result.Fail("Booking not found.");
        if (b.Status != BookingStatus.Confirmed) return Result.Fail("Only a confirmed booking can be checked in.");
        if (Today < b.CheckIn) return Result.Fail($"Check-in is on {b.CheckIn:d MMM}.");
        b.Status = BookingStatus.CheckedIn;
        b.CheckedInAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CheckOutAsync(int bookingId, CancellationToken ct = default)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (b is null) return Result.Fail("Booking not found.");
        if (b.Status != BookingStatus.CheckedIn) return Result.Fail("Only a guest who is checked in can check out.");
        b.Status = BookingStatus.CheckedOut;
        b.CheckedOutAtUtc = Now;
        // Leaving early: the nights not used become free again.
        var today = Today;
        db.BookingNights.RemoveRange(await db.BookingNights.Where(n => n.BookingId == b.Id && n.Night >= today).ToListAsync(ct));
        db.CleaningTasks.Add(new CleaningTask { StayUnitId = b.StayUnitId, BookingId = b.Id, Due = today });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int bookingId, string reason, bool noShow, CancellationToken ct = default)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (b is null) return Result.Fail("Booking not found.");
        if (b.Status is not (BookingStatus.Confirmed or BookingStatus.Held)) return Result.Fail("Only a booking that hasn't started can be cancelled.");
        if (noShow && Today < b.CheckIn) return Result.Fail("A no-show can only be marked from the check-in date.");
        if (string.IsNullOrWhiteSpace(reason) && !noShow) return Result.Fail("Give a reason for cancelling.");
        b.Status = noShow ? BookingStatus.NoShow : BookingStatus.Cancelled;
        b.CancelledAtUtc = Now;
        b.CancellationReason = noShow ? "Guest didn't arrive" : reason.Trim();
        db.BookingNights.RemoveRange(await db.BookingNights.Where(n => n.BookingId == b.Id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ blocks

    public async Task<Result<int>> BlockAsync(int stayUnitId, DateOnly from, DateOnly to, string reason, CancellationToken ct = default)
    {
        var stay = await db.StayUnits.FirstOrDefaultAsync(s => s.Id == stayUnitId, ct);
        if (stay is null) return Result<int>.Fail("Short stay not found.");
        if (to < from) return Result<int>.Fail("The block must end on or after it starts.");
        if (to.DayNumber - from.DayNumber > 365) return Result<int>.Fail("Block at most a year at a time.");
        if (await TakenAsync(stay.UnitId, from, to.AddDays(1), ct) is string taken) return Result<int>.Fail(taken);
        var block = new CalendarBlock { StayUnitId = stay.Id, From = from, To = to, Reason = string.IsNullOrWhiteSpace(reason) ? "Not available" : reason.Trim() };
        db.CalendarBlocks.Add(block);
        for (var d = from; d <= to; d = d.AddDays(1)) db.BookingNights.Add(new BookingNight { UnitId = stay.UnitId, Night = d, CalendarBlock = block });
        if (await SaveGuardedAsync(ct) is string clash) return Result<int>.Fail(clash);
        return Result<int>.Ok(block.Id);
    }

    public async Task<Result> UnblockAsync(int blockId, CancellationToken ct = default)
    {
        var block = await db.CalendarBlocks.FirstOrDefaultAsync(x => x.Id == blockId, ct);
        if (block is null) return Result.Fail("Block not found.");
        db.BookingNights.RemoveRange(await db.BookingNights.Where(n => n.CalendarBlockId == blockId).ToListAsync(ct));
        db.CalendarBlocks.Remove(block);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ payments and messages

    public async Task<Result<int>> RecordPaymentAsync(int bookingId, StayPaymentMethod method, decimal amount, DateOnly receivedOn, string? reference, CancellationToken ct = default)
    {
        var b = await db.Bookings.Include(x => x.Guest).Include(x => x.StayUnit).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == bookingId, ct);
        if (b is null) return Result<int>.Fail("Booking not found.");
        if (b.Status is BookingStatus.Cancelled) return Result<int>.Fail("This booking is cancelled.");
        if (amount <= 0) return Result<int>.Fail("Enter the amount received.");
        if (amount > b.Balance) return Result<int>.Fail($"That's more than the balance of KES {b.Balance:N0}.");
        if (receivedOn > Today) return Result<int>.Fail("The payment date can't be in the future.");
        var code = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim().ToUpperInvariant();
        if (method == StayPaymentMethod.Mpesa && (code is null || code.Length != 10 || !code.All(char.IsLetterOrDigit)))
            return Result<int>.Fail("Enter the 10-character M-PESA code, e.g. SJA9X8Y7Z6.");
        if (code is not null && await db.BookingPayments.AnyAsync(p => p.Reference == code, ct))
            return Result<int>.Fail($"{code} has already been recorded.");

        var payment = new BookingPayment
        {
            BookingId = b.Id, Method = method, Amount = amount, ReceivedOn = receivedOn, Reference = code,
            ReceiptNumber = $"RC-{b.BookingNumber}-{b.Payments.Count + 1}"
        };
        db.BookingPayments.Add(payment);
        b.AmountPaid += amount;
        if (b.Status == BookingStatus.Held && b.AmountPaid >= b.DepositDue)
        {
            b.Status = BookingStatus.Confirmed;   // a guest-site booking is confirmed once the deposit is in
            b.HoldExpiresUtc = null;
        }
        if (await SaveGuardedAsync(ct) is not null) return Result<int>.Fail($"{code} has already been recorded.");

        if (b.Guest.Email is string to && email is not null)
        {
            var business = await BusinessAsync(ct);
            await TrySendAsync(new EmailMessage(to, $"Receipt {payment.ReceiptNumber}: {b.StayUnit.Title}",
                Frame(business.Name, $"""
                    <p>Dear {Html(b.Guest.FullName)},</p>
                    <p>Thank you. We have received <strong>KES {amount:N0}</strong> ({method}{(code is null ? "" : $", {code}")}) for booking <strong>{b.BookingNumber}</strong>, {b.CheckIn:ddd d MMM} to {b.CheckOut:ddd d MMM yyyy}.</p>
                    <p>Total KES {b.Total:N0} · Paid KES {b.AmountPaid:N0} · <strong>Balance KES {b.Balance:N0}</strong></p>
                    <p>Receipt number: {payment.ReceiptNumber}</p>
                    """, business.Phone), business.Email), ct);
        }
        return Result<int>.Ok(payment.Id);
    }

    public async Task<Result> SendConfirmationAsync(int bookingId, CancellationToken ct = default)
    {
        var b = await GetAsync(bookingId, ct);
        if (b is null) return Result.Fail("Booking not found.");
        if (b.Guest.Email is not string to) return Result.Fail("This guest has no email address. Add one, or send the details by SMS or WhatsApp.");
        if (email is null) return Result.Fail("Email isn't set up.");
        var business = await BusinessAsync(ct);
        var due = Math.Max(0, b.DepositDue - b.AmountPaid);
        var howToPay = string.IsNullOrWhiteSpace(business.PaymentInstructions)
            ? $"<p>Please contact us on {Html(business.Phone ?? "")} for how to pay.</p>"
            : $"<p><strong>How to pay:</strong><br/>{Html(business.PaymentInstructions!).Replace("\n", "<br/>")}<br/>Use <strong>{b.BookingNumber}</strong> as the account number or reference.</p>";
        var body = $"""
            <p>Dear {Html(b.Guest.FullName)},</p>
            <p>Your booking <strong>{b.BookingNumber}</strong> is confirmed:</p>
            <p><strong>{Html(b.StayUnit.Title)}</strong><br/>{Html(b.StayUnit.Unit.Asset.Name)}<br/>
               Check-in {b.CheckIn:dddd d MMM yyyy} from {b.StayUnit.CheckInFrom:HH:mm}<br/>
               Check-out {b.CheckOut:dddd d MMM yyyy} by {b.StayUnit.CheckOutBy:HH:mm}<br/>
               {b.Nights} night{(b.Nights == 1 ? "" : "s")}, {b.Adults + b.Children} guest{(b.Adults + b.Children == 1 ? "" : "s")}</p>
            <p>Total KES {b.Total:N0} · Paid KES {b.AmountPaid:N0} · Balance KES {b.Balance:N0}
               {(due > 0 ? $"<br/><strong>Deposit due now: KES {due:N0}</strong>" : "")}</p>
            {howToPay}
            {(string.IsNullOrWhiteSpace(b.StayUnit.HouseRules) ? "" : $"<p><strong>House rules:</strong> {Html(b.StayUnit.HouseRules!)}</p>")}
            """;
        return await TrySendAsync(new EmailMessage(to, $"Booking {b.BookingNumber} confirmed: {b.StayUnit.Title}", Frame(business.Name, body, business.Phone), business.Email), ct)
            ? Result.Ok() : Result.Fail("The email couldn't be sent. Check the email settings, or send the details another way.");
    }

    private sealed record BusinessInfo(string Name, string? Phone, string? Email, string? PaymentInstructions);

    private async Task<BusinessInfo> BusinessAsync(CancellationToken ct)
        => await db.Businesses.AsNoTracking().Where(x => x.Id == currentUser.BusinessId)
               .Select(x => new BusinessInfo(x.Name, x.Phone, x.Email, x.PaymentInstructions)).FirstOrDefaultAsync(ct)
           ?? new BusinessInfo("Your host", null, null, null);

    private async Task<bool> TrySendAsync(EmailMessage message, CancellationToken ct)
    {
        try { await email!.SendAsync(message, ct); return true; }
        catch (Exception) { return false; }   // a failed email never undoes the payment
    }

    private static string Html(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string Frame(string businessName, string body, string? phone)
        => $"""
           <div style="font-family:Arial,sans-serif;font-size:14px;color:#0F1D2E;max-width:560px">
             <h2 style="color:#0B2A4A;margin:0 0 12px">{Html(businessName)}</h2>
             {body}
             <p style="color:#5B6B80;font-size:12px;margin-top:24px">{Html(businessName)}{(phone is null ? "" : $" · {Html(phone)}")} · Booked with ShambaNyumba Stays</p>
           </div>
           """;

    // ------------------------------------------------------------------ cleaning

    public async Task<IReadOnlyList<CleaningItem>> CleaningAsync(bool includeDone, CancellationToken ct = default)
    {
        var q = db.CleaningTasks.AsNoTracking().Where(t => includeDone || t.Status == CleaningStatus.Pending);
        return await q.OrderByDescending(t => t.Status == CleaningStatus.Pending).ThenBy(t => t.Due)
            .Select(t => new CleaningItem(t.Id, t.StayUnit.Title, t.StayUnit.Unit.UnitNumber, t.Due, t.Status,
                db.Bookings.Where(b => b.Id == t.BookingId).Select(b => b.BookingNumber).FirstOrDefault()))
            .Take(200).ToListAsync(ct);
    }

    public async Task<Result> CompleteCleaningAsync(int taskId, CancellationToken ct = default)
    {
        var t = await db.CleaningTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (t is null) return Result.Fail("Task not found.");
        if (t.Status == CleaningStatus.Done) return Result.Ok();
        t.Status = CleaningStatus.Done;
        t.DoneAtUtc = Now;
        t.DoneBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string?> TakenAsync(int unitId, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var first = await db.BookingNights.Where(n => n.UnitId == unitId && n.Night >= from && n.Night < toExclusive)
            .OrderBy(n => n.Night).Select(n => (DateOnly?)n.Night).FirstOrDefaultAsync(ct);
        return first is DateOnly d ? $"Not available: {d:ddd d MMM} is already booked or blocked." : null;
    }

    /// <summary>Saves; if the database's double-booking guard refuses, returns a friendly message instead of an error page.</summary>
    private async Task<string?> SaveGuardedAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return "Those dates were just taken by another booking. Please choose other dates.";
        }
    }

    private async Task<string> NextNumberAsync(CancellationToken ct)
        => $"BK-{await db.Bookings.IgnoreQueryFilters().CountAsync(b => b.BusinessId == currentUser.BusinessId, ct) + 1:00000}";

    private static Task<List<BookingListItem>> Items(IQueryable<Booking> q, CancellationToken ct)
        => q.AsNoTracking().OrderBy(b => b.CheckIn)
            .Select(b => new BookingListItem(b.Id, b.BookingNumber, b.StayUnit.Title, b.Guest.FullName, b.Guest.Phone,
                b.CheckIn, b.CheckOut, b.Nights, b.Status, b.Channel, b.Total, b.AmountPaid))
            .Take(300).ToListAsync(ct);
}
