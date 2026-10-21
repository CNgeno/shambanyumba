using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-08 / NFR-07. THE ONLY code that changes Invoice.AmountPaid, always in the same SaveChanges as the
/// allocation that justifies it. Payments are never updated or deleted, only reversed.
/// Depends on: DbContext, INumberSequenceService, ICurrentUserService, TimeProvider.
/// </summary>
public sealed class PaymentService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    ICurrentUserService currentUser,
    TimeProvider clock) : IPaymentService
{
    private DateOnly Today => clock.TodayEat();

    // ------------------------------------------------------------------ queries

    public async Task<IReadOnlyList<PaymentSummary>> ListAsync(PaymentListFilter filter, CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking();
        if (!filter.IncludeReversed) query = query.Where(p => p.Status == PaymentStatus.Active);
        if (filter.Month is DateOnly month)
        {
            var (start, end) = BillingRules.MonthBounds(month.Year, month.Month);
            query = query.Where(p => p.PaymentDate >= start && p.PaymentDate <= end);
        }
        if (filter.Method is PaymentMethod method) query = query.Where(p => p.Method == method);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.ReceiptNumber.Contains(term) || p.Tenant.FullName.Contains(term)
                                     || (p.Reference != null && p.Reference.Contains(term)));
        }
        return await Project(query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).Take(1000)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentSummary>> ListForInvoiceAsync(int invoiceId, CancellationToken ct = default)
        => await Project(db.Payments.AsNoTracking()
                .Where(p => p.Allocations.Any(a => a.InvoiceId == invoiceId))
                .OrderBy(p => p.PaymentDate))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RefundSummary>> ListRefundsAsync(DateOnly? month, CancellationToken ct = default)
    {
        var query = db.Refunds.AsNoTracking();
        if (month is DateOnly m)
        {
            var (start, end) = BillingRules.MonthBounds(m.Year, m.Month);
            query = query.Where(r => r.RefundDate >= start && r.RefundDate <= end);
        }
        return await ProjectRefunds(query.OrderByDescending(r => r.RefundDate).ThenByDescending(r => r.Id).Take(500)).ToListAsync(ct);
    }

    public async Task<PaymentDetails?> GetDetailsAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking()
            .Include(p => p.Tenant)
            .Include(p => p.Lease).ThenInclude(l => l!.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return null;

        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(a => a.PaymentId == paymentId)
            .OrderBy(a => a.Invoice.DueDate)
            .Select(a => new AllocationLine(a.Id, a.InvoiceId, a.Invoice.InvoiceNumber, a.Invoice.DueDate, a.Amount, a.IsReversed))
            .ToListAsync(ct);
        var refunds = await ProjectRefunds(db.Refunds.AsNoTracking().Where(r => r.PaymentId == paymentId)).ToListAsync(ct);

        var unallocated = payment.Status == PaymentStatus.Active
            ? payment.Amount - allocations.Where(a => !a.IsReversed).Sum(a => a.Amount) - refunds.Sum(r => r.Amount)
            : 0m;
        return new PaymentDetails(payment, allocations, refunds, unallocated, await GetTenantBalanceAsync(payment.TenantId, ct));
    }

    public async Task<IReadOnlyList<OpenInvoice>> GetOpenInvoicesAsync(int tenantId, CancellationToken ct = default)
        => await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total)
            .OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .Select(i => new OpenInvoice(i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.PeriodStart,
                i.Lease != null ? i.Lease.Unit.UnitNumber : null, i.Total, i.AmountPaid))
            .ToListAsync(ct);

    public async Task<TenantBalance> GetTenantBalanceAsync(int tenantId, CancellationToken ct = default)
        => (await GetTenantBalancesAsync([tenantId], ct)).GetValueOrDefault(tenantId) ?? new TenantBalance(0, 0, null, 0);

    public async Task<IReadOnlyDictionary<int, TenantBalance>> GetTenantBalancesAsync(IReadOnlyCollection<int> tenantIds, CancellationToken ct = default)
    {
        var ids = tenantIds.Distinct().ToList();
        var open = await db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.TenantId) && i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total)
            .Select(i => new { i.TenantId, Balance = i.Total - i.AmountPaid })
            .ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.TenantId) && p.Status == PaymentStatus.Active)
            .Select(p => new
            {
                p.TenantId, p.PaymentDate, p.Amount,
                Allocated = p.Allocations.Where(a => !a.IsReversed).Sum(a => (decimal?)a.Amount) ?? 0m
            })
            .ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => ids.Contains(r.TenantId) && r.Kind == RefundKind.Overpayment)
            .Select(r => new { r.TenantId, r.Amount })
            .ToListAsync(ct);

        return ids.ToDictionary(id => id, id =>
        {
            var mine = payments.Where(p => p.TenantId == id).ToList();
            var credit = mine.Sum(p => p.Amount - p.Allocated) - refunds.Where(r => r.TenantId == id).Sum(r => r.Amount);
            var openMine = open.Where(o => o.TenantId == id).ToList();
            return new TenantBalance(openMine.Sum(o => o.Balance), Math.Max(0, credit),
                mine.Count == 0 ? null : mine.Max(p => p.PaymentDate), openMine.Count);
        });
    }

    public async Task<CollectionSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = Today;
        var (start, end) = BillingRules.MonthBounds(today.Year, today.Month);
        var month = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Active && p.PaymentDate >= start && p.PaymentDate <= end)
            .Select(p => p.Amount).ToListAsync(ct);

        var allActive = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Active)
            .Select(p => new { p.Amount, Allocated = p.Allocations.Where(a => !a.IsReversed).Sum(a => (decimal?)a.Amount) ?? 0m })
            .ToListAsync(ct);
        var overpaymentRefunds = await db.Refunds.AsNoTracking().Where(r => r.Kind == RefundKind.Overpayment).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var refundedThisMonth = await db.Refunds.AsNoTracking().Where(r => r.RefundDate >= start && r.RefundDate <= end).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

        return new CollectionSummary(month.Sum(), month.Count,
            Math.Max(0, allActive.Sum(p => p.Amount - p.Allocated) - overpaymentRefunds), refundedThisMonth);
    }

    public async Task<decimal> GetDepositRefundableAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease?.DepositRefundDue is not decimal due) return 0m;
        var refunded = await db.Refunds.AsNoTracking()
            .Where(r => r.LeaseId == leaseId && r.Kind == RefundKind.Deposit)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        return Math.Max(0, due - refunded);
    }

    // ------------------------------------------------------------------ record

    public async Task<Result<int>> RecordAsync(PaymentInput input, CancellationToken ct = default)
    {
        if (input.Amount <= 0) return Result<int>.Fail("Enter the amount received.");
        if (input.PaymentDate > Today) return Result<int>.Fail("A payment can't be dated in the future.");
        if (!await db.Tenants.AnyAsync(t => t.Id == input.TenantId, ct)) return Result<int>.Fail("Choose a tenant.");
        if (input.LeaseId is int lid && !await db.Leases.AnyAsync(l => l.Id == lid && l.TenantId == input.TenantId, ct))
            return Result<int>.Fail("That lease doesn't belong to the chosen tenant.");

        var (reference, refError) = AllocationEngine.NormalizeReference(input.Method, input.Reference);
        if (refError is not null) return Result<int>.Fail(refError);
        if (reference is not null && await db.Payments.AnyAsync(p => p.Method == input.Method && p.Reference == reference && p.Status == PaymentStatus.Active, ct))
            return Result<int>.Fail($"A payment with reference {reference} has already been recorded.");

        var open = await LoadOpenInvoicesAsync(input.TenantId, ct);
        var (plan, planError) = Plan(input.Mode, input.Amount, open, input.ManualAllocations);
        if (planError is not null) return Result<int>.Fail(planError);

        var number = await numbers.NextAsync(SequenceKeys.Receipt, ct);   // before tracking anything new
        open = await LoadOpenInvoicesAsync(input.TenantId, ct, track: true);

        var payment = new Payment
        {
            ReceiptNumber = $"RCT-{number:00000}",
            TenantId = input.TenantId,
            LeaseId = input.LeaseId,
            PaymentDate = input.PaymentDate,
            Amount = input.Amount,
            Method = input.Method,
            Reference = reference,
            PayerName = input.PayerName?.Trim(),
            Notes = input.Notes?.Trim()
        };
        Allocate(payment, plan, open);
        db.Payments.Add(payment);

        var saved = await SaveAsync(ct);
        return saved.Succeeded ? Result<int>.Ok(payment.Id) : Result<int>.Fail(saved.Error!);
    }

    /// <summary>Applies the tenant's unallocated credit (oldest payments first) to their open invoices (oldest first).</summary>
    public async Task<Result<decimal>> ApplyCreditAsync(int tenantId, CancellationToken ct = default)
    {
        var payments = await db.Payments
            .Include(p => p.Allocations)
            .Where(p => p.TenantId == tenantId && p.Status == PaymentStatus.Active)
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
            .ToListAsync(ct);
        var refundsByPayment = await db.Refunds.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.PaymentId != null)
            .GroupBy(r => r.PaymentId!.Value)
            .Select(g => new { PaymentId = g.Key, Amount = g.Sum(r => r.Amount) })
            .ToDictionaryAsync(x => x.PaymentId, x => x.Amount, ct);
        var open = await LoadOpenInvoicesAsync(tenantId, ct, track: true);

        decimal applied = 0;
        foreach (var payment in payments)
        {
            var spare = payment.Amount - payment.Allocations.Where(a => !a.IsReversed).Sum(a => a.Amount)
                        - refundsByPayment.GetValueOrDefault(payment.Id);
            if (spare <= 0) continue;

            var items = open.Where(i => i.Total - i.AmountPaid > 0).Select(ToItem).ToList();
            if (items.Count == 0) break;

            var (plan, _) = AllocationEngine.OldestFirst(spare, items);
            Allocate(payment, plan, open);
            applied += plan.Sum(p => p.Amount);
        }

        if (applied == 0) return Result<decimal>.Fail("There's no credit to apply, or no unpaid invoices to apply it to.");
        var saved = await SaveAsync(ct);
        return saved.Succeeded ? Result<decimal>.Ok(applied) : Result<decimal>.Fail(saved.Error!);
    }

    // ------------------------------------------------------------------ reverse

    public async Task<Result> ReverseAsync(int paymentId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason, e.g. cheque bounced or recorded against the wrong tenant.");

        var payment = await db.Payments.Include(p => p.Allocations).ThenInclude(a => a.Invoice).FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status == PaymentStatus.Reversed) return Result.Fail("This payment has already been reversed.");
        if (await db.Refunds.AnyAsync(r => r.PaymentId == paymentId, ct))
            return Result.Fail("Part of this payment has been refunded, so it can't be reversed.");

        payment.Status = PaymentStatus.Reversed;
        payment.ReversedAtUtc = clock.GetUtcNow().UtcDateTime;
        payment.ReversedBy = currentUser.UserId;
        payment.ReversalReason = reason.Trim();

        foreach (var allocation in payment.Allocations.Where(a => !a.IsReversed))
        {
            allocation.IsReversed = true;
            allocation.Invoice.AmountPaid -= allocation.Amount;   // the invoice is owed again
        }
        return await SaveAsync(ct);
    }

    // ------------------------------------------------------------------ refunds

    public async Task<Result<int>> RefundOverpaymentAsync(int paymentId, RefundInput input, CancellationToken ct = default)
    {
        var details = await GetDetailsAsync(paymentId, ct);
        if (details is null) return Result<int>.Fail("Payment not found.");
        if (details.Payment.Status != PaymentStatus.Active) return Result<int>.Fail("This payment has been reversed.");

        var error = ValidateRefund(input, details.Unallocated, "This payment only has {0} unallocated.");
        if (error is not null) return Result<int>.Fail(error);

        return await AddRefundAsync(new Refund
        {
            Kind = RefundKind.Overpayment, TenantId = details.Payment.TenantId, LeaseId = details.Payment.LeaseId, PaymentId = paymentId
        }, input, ct);
    }

    public async Task<Result<int>> RefundDepositAsync(int leaseId, RefundInput input, CancellationToken ct = default)
    {
        var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result<int>.Fail("Lease not found.");
        if (lease.Status is not (LeaseStatus.Terminated or LeaseStatus.Expired))
            return Result<int>.Fail("A deposit is refunded once the lease has ended. Renewed leases carry the deposit over.");

        var error = ValidateRefund(input, await GetDepositRefundableAsync(leaseId, ct), "Only {0} of the deposit is still due back.");
        if (error is not null) return Result<int>.Fail(error);

        return await AddRefundAsync(new Refund { Kind = RefundKind.Deposit, TenantId = lease.TenantId, LeaseId = leaseId }, input, ct);
    }

    // ------------------------------------------------------------------ helpers

    private string? ValidateRefund(RefundInput input, decimal available, string tooMuch)
    {
        if (input.Amount <= 0) return "Enter the amount refunded.";
        if (input.Amount > available) return string.Format(tooMuch, $"KES {available:N2}");
        if (input.RefundDate > Today) return "A refund can't be dated in the future.";
        return input.Method != PaymentMethod.Cash && string.IsNullOrWhiteSpace(input.Reference)
            ? "Enter the M-PESA, bank or cheque reference of the refund." : null;
    }

    private async Task<Result<int>> AddRefundAsync(Refund refund, RefundInput input, CancellationToken ct)
    {
        var number = await numbers.NextAsync(SequenceKeys.Refund, ct);
        refund.RefundNumber = $"RFD-{number:00000}";
        refund.RefundDate = input.RefundDate;
        refund.Amount = input.Amount;
        refund.Method = input.Method;
        refund.Reference = input.Reference?.Trim().ToUpperInvariant();
        refund.Notes = input.Notes?.Trim();
        db.Refunds.Add(refund);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(refund.Id);
    }

    private Task<List<Invoice>> LoadOpenInvoicesAsync(int tenantId, CancellationToken ct, bool track = false)
    {
        var query = db.Invoices.Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total);
        return (track ? query : query.AsNoTracking()).OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceDate).ThenBy(i => i.Id).ToListAsync(ct);
    }

    private static OpenInvoiceItem ToItem(Invoice i) => new(i.Id, i.DueDate, i.InvoiceDate, i.Total - i.AmountPaid);

    private static (IReadOnlyList<(int InvoiceId, decimal Amount)> Plan, string? Error) Plan(
        AllocationMode mode, decimal amount, IReadOnlyList<Invoice> open, IReadOnlyDictionary<int, decimal>? manual)
    {
        switch (mode)
        {
            case AllocationMode.KeepAsCredit:
                return ([], null);
            case AllocationMode.Manual:
                var requested = (manual ?? new Dictionary<int, decimal>()).Where(kv => kv.Value != 0).ToDictionary(kv => kv.Key, kv => kv.Value);
                var error = AllocationEngine.ValidateManual(amount, requested, open.ToDictionary(i => i.Id, i => i.Total - i.AmountPaid));
                return error is null ? (requested.Select(kv => (kv.Key, kv.Value)).ToList(), null) : ([], error);
            default:
                return (AllocationEngine.OldestFirst(amount, open.Select(ToItem)).Allocations, null);
        }
    }

    /// <summary>Adds allocations and raises AmountPaid on the (tracked) invoices, re-checking balances.</summary>
    private static void Allocate(Payment payment, IReadOnlyList<(int InvoiceId, decimal Amount)> plan, IReadOnlyList<Invoice> open)
    {
        foreach (var (invoiceId, amount) in plan.Where(p => p.Amount > 0))
        {
            var invoice = open.First(i => i.Id == invoiceId);
            var take = Math.Min(amount, invoice.Total - invoice.AmountPaid);
            if (take <= 0) continue;
            invoice.AmountPaid += take;
            payment.Allocations.Add(new PaymentAllocation { Invoice = invoice, Amount = take });
        }
    }

    private async Task<Result> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Result.Fail("One of the invoices was updated by someone else at the same moment. Nothing was saved; please try again.");
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Result.Fail("That reference has already been recorded.");
        }
    }

    private IQueryable<PaymentSummary> Project(IQueryable<Payment> query)
        => query.Select(p => new PaymentSummary(p.Id, p.ReceiptNumber, p.TenantId, p.Tenant.FullName, p.PaymentDate, p.Amount,
            p.Method, p.Reference,
            p.Allocations.Where(a => !a.IsReversed).Sum(a => (decimal?)a.Amount) ?? 0m,
            db.Refunds.Where(r => r.PaymentId == p.Id).Sum(r => (decimal?)r.Amount) ?? 0m,
            p.Status));

    private static IQueryable<RefundSummary> ProjectRefunds(IQueryable<Refund> query)
        => query.Select(r => new RefundSummary(r.Id, r.RefundNumber, r.Kind, r.TenantId, r.Tenant.FullName, r.LeaseId,
            r.Lease != null ? r.Lease.LeaseNumber : null, r.PaymentId, r.Payment != null ? r.Payment.ReceiptNumber : null,
            r.RefundDate, r.Amount, r.Method, r.Reference, r.Notes));
}
