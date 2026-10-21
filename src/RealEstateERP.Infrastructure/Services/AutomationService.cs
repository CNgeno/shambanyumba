using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// ORCHESTRATOR for the current business: monthly invoices (once a month, on or after the chosen day), late penalties
/// (one per overdue invoice, as in the manual preview) and invoice emails. The billing run and penalty run clear the
/// change tracker as they go, so the business row is always re-read before it's updated.
/// </summary>
public sealed class AutomationService(
    IBillingRunService billing,
    IArrearsService arrears,
    ITenantMessagingService messaging,
    IListingService listings,
    ApplicationDbContext db,
    ISystemConfigService config,
    ICurrentUserService currentUser,
    TimeProvider clock) : IAutomationService
{
    private int BusinessId => currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");

    public async Task<AutomationStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var b = await db.Businesses.AsNoTracking().FirstAsync(x => x.Id == BusinessId, ct);
        return new AutomationStatus(
            new AutomationSettings(b.AutoInvoiceEnabled, b.AutoInvoiceDay, b.AutoPenaltiesEnabled, b.PenaltyGraceDays, b.PenaltyPercent,
                b.EmailInvoicesToTenants, b.EmailReceiptsToTenants, b.PaymentInstructions, b.AutoListVacantUnits),
            b.LastAutomationRunOn, b.LastAutoInvoiceMonth, b.LastAutomationSummary,
            await config.GetIntAsync(ConfigKeys.DefaultGraceDaysAfterDue, 5, ct),
            await config.GetDecimalAsync(ConfigKeys.DefaultPenaltyPercent, 5m, ct));
    }

    public async Task<Result> SaveSettingsAsync(AutomationSettings s, CancellationToken ct = default)
    {
        if (s.AutoInvoiceDay is < 1 or > 28) return Result.Fail("Choose an invoicing day between 1 and 28.");
        if (s.PenaltyGraceDays is < 0 or > 60) return Result.Fail("Grace days must be between 0 and 60.");
        if (s.PenaltyPercent is < 0 or > 50) return Result.Fail("The penalty must be between 0 and 50%.");
        if (s.PaymentInstructions?.Length > 1000) return Result.Fail("Keep the payment instructions under 1,000 characters.");

        var b = await db.Businesses.FirstAsync(x => x.Id == BusinessId, ct);
        b.AutoInvoiceEnabled = s.AutoInvoiceEnabled;
        b.AutoInvoiceDay = s.AutoInvoiceDay;
        b.AutoPenaltiesEnabled = s.AutoPenaltiesEnabled;
        b.PenaltyGraceDays = s.PenaltyGraceDays;
        b.PenaltyPercent = s.PenaltyPercent;
        b.EmailInvoicesToTenants = s.EmailInvoicesToTenants;
        b.EmailReceiptsToTenants = s.EmailReceiptsToTenants;
        b.PaymentInstructions = string.IsNullOrWhiteSpace(s.PaymentInstructions) ? null : s.PaymentInstructions.Trim();
        b.AutoListVacantUnits = s.AutoListVacantUnits;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<AutomationRunResult> RunAsync(CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var today = clock.TodayEat();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var settings = await db.Businesses.AsNoTracking().Where(b => b.Id == businessId)
            .Select(b => new { b.AutoInvoiceEnabled, b.AutoInvoiceDay, b.AutoPenaltiesEnabled, b.EmailInvoicesToTenants, b.LastAutoInvoiceMonth, b.AutoListVacantUnits })
            .FirstAsync(ct);

        if (!settings.AutoInvoiceEnabled && !settings.AutoPenaltiesEnabled && !settings.AutoListVacantUnits)
            return new AutomationRunResult(false, 0, 0, 0, 0, ["Automation is switched off for this business."]);

        int created = 0, emailed = 0, penalties = 0, listed = 0;
        decimal penaltyTotal = 0;
        var notes = new List<string>();
        var invoicedThisMonth = false;
        var started = clock.GetUtcNow().UtcDateTime;

        // ---- monthly invoices ----
        if (settings.AutoInvoiceEnabled && today.Day >= settings.AutoInvoiceDay && settings.LastAutoInvoiceMonth != monthStart)
        {
            var run = await billing.GenerateAsync(new BillingRunRequest(today.Year, today.Month, null, IncludeDeposits: true, IssueImmediately: true), ct);
            created = run.Created;
            notes.AddRange(run.Problems.Take(3));
            // A run refused outright (e.g. not enough credits) is tried again tomorrow; otherwise the month is done.
            invoicedThisMonth = run.Created > 0 || run.Problems.Count == 0;
        }

        // ---- late penalties ----
        if (settings.AutoPenaltiesEnabled)
        {
            var result = await arrears.ApplyPenaltiesAsync(null, ct);
            penalties = result.Created;
            penaltyTotal = result.Total;
            notes.AddRange(result.Problems.Take(3));
        }

        // ---- email what was just issued ----
        if (settings.EmailInvoicesToTenants && (created > 0 || penalties > 0))
        {
            db.ChangeTracker.Clear();
            var toSend = await db.Invoices.AsNoTracking()
                .Where(i => i.Status == InvoiceStatus.Pending && i.EmailedAtUtc == null && i.IssuedAtUtc >= started
                            && i.Tenant.Email != null && i.Tenant.Email != "")
                .Select(i => i.Id).ToListAsync(ct);
            var failures = 0;
            foreach (var id in toSend)
            {
                if ((await messaging.SendInvoiceAsync(id, ct)).Succeeded) emailed++;
                else failures++;
            }
            if (failures > 0) notes.Add($"{failures} invoice email(s) couldn't be sent; check Admin > Settings > Email.");
        }

        // ---- advertise vacant units ----
        if (settings.AutoListVacantUnits)
        {
            db.ChangeTracker.Clear();
            listed = await listings.AutoListVacantAsync(ct);
        }

        var outcome = new AutomationRunResult(true, created, emailed, penalties, penaltyTotal, notes, listed);
        db.ChangeTracker.Clear();
        if (created > 0 || penalties > 0 || listed > 0 || notes.Count > 0)
            NotificationLog.Add(db, businessId, NotificationType.Automation, "Automatic run", outcome.Summary, created > 0 ? "/Invoices" : "/Arrears");

        // Weekly reminder of leases ending within 30 days.
        var weekAgo = clock.GetUtcNow().UtcDateTime.AddDays(-7);
        if (!await db.Notifications.AnyAsync(n => n.BusinessId == businessId && n.Type == NotificationType.LeaseExpiring && n.CreatedAtUtc > weekAgo, ct))
        {
            var ending = await db.Leases.CountAsync(l => l.Status == LeaseStatus.Active && l.EndDate != null && l.EndDate >= today && l.EndDate <= today.AddDays(30), ct);
            if (ending > 0)
                NotificationLog.Add(db, businessId, NotificationType.LeaseExpiring, $"{ending} lease(s) end within 30 days", "Renew them or plan for vacancies.", "/Leases");
        }

        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, ct);
        if (invoicedThisMonth) business.LastAutoInvoiceMonth = monthStart;
        business.LastAutomationSummary = $"{today:d MMM yyyy}: {outcome.Summary}";
        if (business.LastAutomationSummary.Length > 1000) business.LastAutomationSummary = business.LastAutomationSummary[..1000];
        await db.SaveChangesAsync(ct);
        return outcome;
    }

    public async Task<bool> TryClaimDailyRunAsync(CancellationToken ct = default)
    {
        var today = clock.TodayEat();
        var business = await db.Businesses.FirstAsync(b => b.Id == BusinessId, ct);
        if (business.LastAutomationRunOn == today) return false;
        business.LastAutomationRunOn = today;
        try
        {
            await db.SaveChangesAsync(ct);   // rowversion: if another server claimed it first, this fails
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<IReadOnlyList<int>> GetAutomatedBusinessIdsAsync(CancellationToken ct = default)
        => await db.Businesses.AsNoTracking().Where(b => b.AutoInvoiceEnabled || b.AutoPenaltiesEnabled || b.AutoListVacantUnits).Select(b => b.Id).ToListAsync(ct);
}
