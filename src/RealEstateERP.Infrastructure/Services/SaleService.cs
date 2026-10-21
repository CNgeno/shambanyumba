using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-11. Depends on: DbContext, INumberSequenceService, IAgentScopeService, IFileStorage, ISystemConfigService, TimeProvider.
/// Restricted agents see and work only on sales assigned to them; sales they create are assigned to them.
/// </summary>
public sealed class SaleService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    IAgentScopeService agentScope,
    IFileStorage storage,
    ISystemConfigService config,
    ICurrentUserService currentUser,
    TimeProvider clock) : ISaleService
{
    private const string PrincipalOnly = "Only the principal, owner or an admin can do this.";
    private DateOnly Today => clock.TodayEat();

    private async Task<IQueryable<Sale>> ScopedSalesAsync(CancellationToken ct)
    {
        var scope = await agentScope.GetAsync(ct);
        return scope.IsRestricted ? db.Sales.Where(s => s.AgentId == scope.AgentId) : db.Sales;
    }

    // ------------------------------------------------------------------ buyers

    public async Task<IReadOnlyList<BuyerSummary>> ListBuyersAsync(string? search, CancellationToken ct = default)
    {
        var scope = await agentScope.GetAsync(ct);
        var query = db.Buyers.AsNoTracking();
        if (scope.IsRestricted)   // their buyers, plus new buyers not yet on any sale
            query = query.Where(b => db.Sales.Any(s => s.BuyerId == b.Id && s.AgentId == scope.AgentId) || !db.Sales.Any(s => s.BuyerId == b.Id));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(b => b.FullName.Contains(search.Trim()) || (b.Phone != null && b.Phone.Contains(search.Trim())));

        var buyers = await query.OrderBy(b => b.FullName).Select(b => new { b.Id, b.FullName, b.Type, b.Phone, b.Email }).ToListAsync(ct);
        var ids = buyers.Select(b => b.Id).ToList();
        var sales = await db.Sales.AsNoTracking()
            .Where(s => ids.Contains(s.BuyerId) && s.Status != SaleStatus.Cancelled)
            .Select(s => new { s.BuyerId, Balance = s.Price - s.AmountPaid }).ToListAsync(ct);
        return buyers.Select(b => new BuyerSummary(b.Id, b.FullName, b.Type, b.Phone, b.Email,
            sales.Count(s => s.BuyerId == b.Id), sales.Where(s => s.BuyerId == b.Id).Sum(s => s.Balance))).ToList();
    }

    public async Task<Buyer?> GetBuyerAsync(int id, CancellationToken ct = default)
    {
        var scope = await agentScope.GetAsync(ct);
        return await db.Buyers.AsNoTracking()
            .Where(b => !scope.IsRestricted || db.Sales.Any(s => s.BuyerId == b.Id && s.AgentId == scope.AgentId) || !db.Sales.Any(s => s.BuyerId == b.Id))
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Result<int>> SaveBuyerAsync(int? id, BuyerInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.FullName)) return Result<int>.Fail("Enter the buyer's name.");
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && (phone = KenyanPhone.Normalize(input.Phone)) is null) return Result<int>.Fail("Enter a valid phone number.");

        Buyer buyer;
        if (id is int existing)
        {
            var found = await GetBuyerAsync(existing, ct) is null ? null : await db.Buyers.FirstOrDefaultAsync(b => b.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Buyer not found.");
            buyer = found;
        }
        else
        {
            buyer = new Buyer();
            db.Buyers.Add(buyer);
        }
        buyer.Type = input.Type;
        buyer.FullName = input.FullName.Trim();
        buyer.IdNumber = input.IdNumber?.Trim().ToUpperInvariant();
        buyer.KraPin = input.KraPin?.Trim().ToUpperInvariant();
        buyer.Phone = phone;
        buyer.Email = input.Email?.Trim().ToLowerInvariant();
        buyer.PostalAddress = input.PostalAddress?.Trim();
        buyer.Notes = input.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(buyer.Id);
    }

    // ------------------------------------------------------------------ sales

    public async Task<IReadOnlyList<SaleSummary>> ListAsync(SalesListView view, int? buyerId, int? assetId, string? search, CancellationToken ct = default)
    {
        var today = Today;
        var query = (await ScopedSalesAsync(ct)).AsNoTracking();
        query = view switch
        {
            SalesListView.Open => query.Where(s => s.Status == SaleStatus.Reserved || s.Status == SaleStatus.Active),
            SalesListView.Reserved => query.Where(s => s.Status == SaleStatus.Reserved),
            SalesListView.Active => query.Where(s => s.Status == SaleStatus.Active),
            SalesListView.Completed => query.Where(s => s.Status == SaleStatus.Completed),
            SalesListView.Cancelled => query.Where(s => s.Status == SaleStatus.Cancelled),
            _ => query
        };
        if (buyerId is int b) query = query.Where(s => s.BuyerId == b);
        if (assetId is int a) query = query.Where(s => s.Unit.AssetId == a);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.SaleNumber.Contains(term) || s.Buyer.FullName.Contains(term) || s.Unit.UnitNumber.Contains(term));
        }
        // Order on the entity BEFORE projecting: SQL Server can't sort by a property of a record built with a constructor.
        query = query.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).Take(1000);
        return await Project(query, today).ToListAsync(ct);
    }

    public async Task<SaleDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        var today = Today;
        var sale = await (await ScopedSalesAsync(ct)).AsNoTracking()
            .Include(s => s.Buyer).Include(s => s.Agent).Include(s => s.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null) return null;

        var schedule = (await db.SaleInstalments.AsNoTracking().Where(i => i.SaleId == id).OrderBy(i => i.Number).ToListAsync(ct))
            .Select(i => new InstalmentRow(i.Id, i.Number, i.Description, i.DueDate, i.Amount, i.AmountPaid, SalesRules.State(i.Amount, i.AmountPaid, i.DueDate, today)))
            .ToList();
        var milestones = await db.SaleMilestones.AsNoTracking().Where(m => m.SaleId == id).OrderBy(m => m.Type).ToListAsync(ct);
        var payments = await db.SalePayments.AsNoTracking().Where(p => p.SaleId == id).OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new SalePaymentRow(p.Id, p.ReceiptNumber, p.PaymentDate, p.Amount, p.Method, p.Reference, p.Status, p.ReversalReason))
            .ToListAsync(ct);
        var overdue = sale.Status is SaleStatus.Cancelled ? 0 : schedule.Where(i => i.State == InstalmentState.Overdue).Sum(i => i.Balance);
        return new SaleDetails(sale, schedule, milestones, payments, overdue);
    }

    public async Task<IReadOnlyList<SellableUnit>> GetSellableUnitsAsync(CancellationToken ct = default)
        => (await db.Units.AsNoTracking()
                .Where(u => u.Status == UnitStatus.Available && !db.Sales.Any(s => s.UnitId == u.Id && s.Status != SaleStatus.Cancelled)
                            // only units in properties for sale (or both)
                            && (u.Asset.Purpose == PropertyPurpose.Sell || u.Asset.Purpose == PropertyPurpose.Both
                                || (u.Asset.Purpose == null && u.Asset.Type == AssetType.Land)))
                .Select(u => new SellableUnit(u.Id, u.UnitNumber, u.AssetId, u.Asset.Name, u.SalePrice, u.Status))
                .ToListAsync(ct))
            .OrderBy(u => u.AssetName).ThenBy(u => u.UnitNumber, NaturalComparer.Instance).ToList();

    public Task<IReadOnlyList<ScheduleLine>> PreviewScheduleAsync(SaleInput input, CancellationToken ct = default)
        => Task.FromResult(SalesRules.ValidatePlan(input.Price, input.DepositAmount, input.PaymentPlan, input.InstalmentCount, input.SaleDate, input.FirstInstalmentDate) is null
            ? SalesRules.BuildSchedule(input.Price, input.DepositAmount, input.PaymentPlan, input.InstalmentCount, input.InstalmentFrequency, input.SaleDate, input.FirstInstalmentDate)
            : (IReadOnlyList<ScheduleLine>)[]);

    public async Task<Result<int>> CreateAsync(SaleInput input, CancellationToken ct = default)
    {
        var scope = await agentScope.GetAsync(ct);
        var agentId = scope.IsRestricted ? scope.AgentId : input.AgentId;   // an agent's own sales are theirs

        var error = SalesRules.ValidatePlan(input.Price, input.DepositAmount, input.PaymentPlan, input.InstalmentCount, input.SaleDate, input.FirstInstalmentDate);
        if (error is not null) return Result<int>.Fail(error);
        if (input.SaleDate > Today) return Result<int>.Fail("The sale date can't be in the future.");
        if (input.CommissionPercent is < 0 or > 20) return Result<int>.Fail("Commission must be between 0 and 20%.");
        if (!await db.Buyers.AnyAsync(b => b.Id == input.BuyerId, ct)) return Result<int>.Fail("Choose the buyer.");
        if (agentId is int a && !await db.Agents.AnyAsync(x => x.Id == a, ct)) return Result<int>.Fail("Choose a valid agent.");

        var unitCheck = await db.Units.AsNoTracking().Where(u => u.Id == input.UnitId).Select(u => new { u.Status }).FirstOrDefaultAsync(ct);
        if (unitCheck is null) return Result<int>.Fail("Choose the unit.");
        if (unitCheck.Status != UnitStatus.Available) return Result<int>.Fail("Only available units can be sold. This one is " + unitCheck.Status.ToString().ToLowerInvariant() + ".");
        if (await db.Sales.AnyAsync(s => s.UnitId == input.UnitId && s.Status != SaleStatus.Cancelled, ct)) return Result<int>.Fail("This unit already has a sale.");

        var number = await numbers.NextAsync(SequenceKeys.Sale, ct);
        var unit = await db.Units.FirstAsync(u => u.Id == input.UnitId, ct);
        var deposit = input.PaymentPlan == PaymentPlan.FullPayment ? input.Price : input.DepositAmount;
        var sale = new Sale
        {
            SaleNumber = $"SL-{number:00000}", BuyerId = input.BuyerId, UnitId = unit.Id, AgentId = agentId, SaleDate = input.SaleDate,
            Price = input.Price, DepositAmount = deposit, PaymentPlan = input.PaymentPlan,
            InstalmentCount = input.PaymentPlan == PaymentPlan.FullPayment ? 0 : input.InstalmentCount,
            InstalmentFrequency = input.InstalmentFrequency,
            FirstInstalmentDate = input.PaymentPlan == PaymentPlan.FullPayment ? null : input.FirstInstalmentDate,
            CommissionPercent = input.CommissionPercent, Notes = input.Notes?.Trim(), Status = SaleStatus.Reserved
        };
        foreach (var line in SalesRules.BuildSchedule(input.Price, deposit, input.PaymentPlan, sale.InstalmentCount, input.InstalmentFrequency, input.SaleDate, sale.FirstInstalmentDate))
            sale.Instalments.Add(new SaleInstalment { Number = line.Number, Description = line.Description, DueDate = line.DueDate, Amount = line.Amount });
        foreach (var type in Enum.GetValues<MilestoneType>())
            sale.Milestones.Add(new SaleMilestone { Type = type });

        unit.Status = UnitStatus.Reserved;
        db.Sales.Add(sale);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result<int>.Fail("This unit already has a sale."); }
        return Result<int>.Ok(sale.Id);
    }

    public async Task<Result> CancelAsync(int id, string reason, bool makeUnitInactive, CancellationToken ct = default)
    {
        if ((await agentScope.GetAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for cancelling.");
        var sale = await db.Sales.Include(s => s.Unit).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sale is null) return Result.Fail("Sale not found.");
        if (sale.Status is SaleStatus.Completed or SaleStatus.Cancelled) return Result.Fail($"A {sale.Status.ToString().ToLowerInvariant()} sale can't be cancelled.");
        if (sale.AmountPaid > 0)
            return Result.Fail($"The buyer has paid KES {sale.AmountPaid:N2}. Reverse those payments (and refund the buyer) before cancelling.");

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledOn = Today;
        sale.CancelReason = reason.Trim();
        sale.Unit.Status = makeUnitInactive ? UnitStatus.Inactive : UnitStatus.Available;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ payments

    public async Task<Result<int>> RecordPaymentAsync(int saleId, SalePaymentInput input, CancellationToken ct = default)
    {
        var check = await (await ScopedSalesAsync(ct)).AsNoTracking().Where(s => s.Id == saleId)
            .Select(s => new { s.Status, Balance = s.Price - s.AmountPaid }).FirstOrDefaultAsync(ct);
        if (check is null) return Result<int>.Fail("Sale not found.");
        if (check.Status is not (SaleStatus.Reserved or SaleStatus.Active)) return Result<int>.Fail("Payments can only be recorded on reserved or active sales.");
        if (input.Amount <= 0) return Result<int>.Fail("Enter the amount received.");
        if (input.Amount > check.Balance) return Result<int>.Fail($"That's more than the balance of KES {check.Balance:N2}.");
        if (input.PaymentDate > Today) return Result<int>.Fail("A payment can't be dated in the future.");

        var (reference, refError) = AllocationEngine.NormalizeReference(input.Method, input.Reference);
        if (refError is not null) return Result<int>.Fail(refError);
        if (reference is not null && await db.SalePayments.AnyAsync(p => p.Method == input.Method && p.Reference == reference && p.Status == PaymentStatus.Active, ct))
            return Result<int>.Fail($"A payment with reference {reference} has already been recorded.");

        var number = await numbers.NextAsync(SequenceKeys.SaleReceipt, ct);
        var sale = await db.Sales.Include(s => s.Instalments).Include(s => s.Milestones).FirstAsync(s => s.Id == saleId, ct);
        var payment = new SalePayment
        {
            ReceiptNumber = $"SR-{number:00000}", SaleId = saleId, PaymentDate = input.PaymentDate, Amount = input.Amount,
            Method = input.Method, Reference = reference, Notes = input.Notes?.Trim()
        };

        var open = sale.Instalments.Where(i => i.Amount > i.AmountPaid).Select(i => new OpenInvoiceItem(i.Id, i.DueDate, i.DueDate, i.Amount - i.AmountPaid)).ToList();
        var (plan, _) = AllocationEngine.OldestFirst(input.Amount, open);
        foreach (var (instalmentId, amount) in plan)
        {
            var instalment = sale.Instalments.First(i => i.Id == instalmentId);
            instalment.AmountPaid += amount;
            payment.Allocations.Add(new SaleAllocation { Instalment = instalment, Amount = amount });
        }
        sale.AmountPaid += input.Amount;
        MaybeComplete(sale);
        db.SalePayments.Add(payment);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Result<int>.Fail("This sale was updated by someone else at the same moment. Nothing was saved; try again."); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Result<int>.Fail("That reference has already been recorded."); }
        return Result<int>.Ok(payment.Id);
    }

    public async Task<Result> ReversePaymentAsync(int salePaymentId, string reason, CancellationToken ct = default)
    {
        if ((await agentScope.GetAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason, e.g. cheque bounced.");
        var payment = await db.SalePayments.Include(p => p.Allocations).ThenInclude(a => a.Instalment).Include(p => p.Sale)
            .FirstOrDefaultAsync(p => p.Id == salePaymentId, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status == PaymentStatus.Reversed) return Result.Fail("Already reversed.");

        payment.Status = PaymentStatus.Reversed;
        payment.ReversedAtUtc = clock.GetUtcNow().UtcDateTime;
        payment.ReversedBy = currentUser.UserId;
        payment.ReversalReason = reason.Trim();
        foreach (var a in payment.Allocations.Where(a => !a.IsReversed))
        {
            a.IsReversed = true;
            a.Instalment.AmountPaid -= a.Amount;
        }
        payment.Sale.AmountPaid -= payment.Amount;
        if (payment.Sale.Status == SaleStatus.Completed) { payment.Sale.Status = SaleStatus.Active; payment.Sale.CompletedOn = null; }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail("This sale was updated by someone else at the same moment. Refresh and try again."); }
        return Result.Ok();
    }

    // ------------------------------------------------------------------ milestones

    public async Task<Result> CompleteMilestoneAsync(int saleId, MilestoneType type, DateOnly completedOn, string? reference, string? notes,
        UploadedFile? document, CancellationToken ct = default)
    {
        var sale = await (await ScopedSalesAsync(ct)).Include(s => s.Milestones).Include(s => s.Unit).FirstOrDefaultAsync(s => s.Id == saleId, ct);
        if (sale is null) return Result.Fail("Sale not found.");
        if (sale.Status is not (SaleStatus.Reserved or SaleStatus.Active)) return Result.Fail("This sale is closed.");
        if (completedOn > Today || completedOn < sale.SaleDate) return Result.Fail("The date must be between the sale date and today.");

        var done = sale.Milestones.Where(m => m.CompletedOn != null).Select(m => m.Type).ToList();
        var error = SalesRules.MilestoneError(type, done, sale.Balance);
        if (error is not null) return Result.Fail(error);

        var milestone = sale.Milestones.First(m => m.Type == type);
        if (document is not null)
        {
            var maxMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct);
            var docError = UploadValidator.Validate(document.Content, document.FileName, document.Length, maxMb);
            if (docError is not null) return Result.Fail(docError);
            milestone.DocumentStorageKey = await storage.SaveAsync(document.Content, "sale-documents", Path.GetExtension(document.FileName), ct);
            milestone.DocumentFileName = Path.GetFileName(document.FileName);
        }
        milestone.CompletedOn = completedOn;
        milestone.Reference = reference?.Trim();
        milestone.Notes = notes?.Trim();

        if (type == MilestoneType.Agreement)
        {
            sale.Status = SaleStatus.Active;      // agreement signed: the unit is sold
            sale.Unit.Status = UnitStatus.Sold;
        }
        MaybeComplete(sale);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<SaleMilestone?> GetMilestoneAsync(int milestoneId, CancellationToken ct = default)
    {
        var sales = await ScopedSalesAsync(ct);
        return await db.SaleMilestones.AsNoTracking().FirstOrDefaultAsync(m => m.Id == milestoneId && sales.Any(s => s.Id == m.SaleId), ct);
    }

    // ------------------------------------------------------------------ reports

    public async Task<SalesSummaryTotals> GetTotalsAsync(CancellationToken ct = default)
    {
        var today = Today;
        var sales = await ScopedSalesAsync(ct);
        var rows = await sales.AsNoTracking().Where(s => s.Status != SaleStatus.Cancelled)
            .Select(s => new
            {
                s.Status, s.Price, s.AmountPaid,
                Overdue = s.Instalments.Where(i => i.DueDate < today && i.AmountPaid < i.Amount).Sum(i => (decimal?)(i.Amount - i.AmountPaid)) ?? 0m
            }).ToListAsync(ct);
        var (start, end) = BillingRules.MonthBounds(today.Year, today.Month);
        var collectedThisMonth = await db.SalePayments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Active && p.PaymentDate >= start && p.PaymentDate <= end && sales.Any(s => s.Id == p.SaleId))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new SalesSummaryTotals(
            rows.Count(r => r.Status == SaleStatus.Reserved), rows.Count(r => r.Status == SaleStatus.Active), rows.Count(r => r.Status == SaleStatus.Completed),
            rows.Where(r => r.Status != SaleStatus.Reserved).Sum(r => r.Price), rows.Sum(r => r.AmountPaid),
            rows.Where(r => r.Status != SaleStatus.Completed).Sum(r => r.Price - r.AmountPaid),
            rows.Sum(r => r.Overdue), rows.Count(r => r.Overdue > 0), collectedThisMonth);
    }

    public async Task<IReadOnlyList<InventoryRow>> GetInventoryAsync(CancellationToken ct = default)
    {
        var units = await db.Units.AsNoTracking()
            .Where(u => u.SalePrice != null || db.Sales.Any(s => s.UnitId == u.Id))
            .Select(u => new { u.AssetId, AssetName = u.Asset.Name, u.Status, u.SalePrice })
            .ToListAsync(ct);
        return units.GroupBy(u => (u.AssetId, u.AssetName))
            .Select(g => new InventoryRow(g.Key.AssetId, g.Key.AssetName, g.Count(),
                g.Count(u => u.Status == UnitStatus.Available), g.Count(u => u.Status == UnitStatus.Reserved), g.Count(u => u.Status == UnitStatus.Sold),
                g.Where(u => u.Status == UnitStatus.Available).Sum(u => u.SalePrice ?? 0)))
            .OrderBy(r => r.AssetName).ToList();
    }

    public async Task<IReadOnlyList<ArrearsSaleRow>> GetInstalmentArrearsAsync(CancellationToken ct = default)
    {
        var today = Today;
        var open = await ListAsync(SalesListView.Open, null, null, null, ct);
        var withArrears = open.Where(s => s.OverdueInstalments > 0).ToList();
        var ids = withArrears.Select(s => s.Id).ToList();
        var overdue = await db.SaleInstalments.AsNoTracking()
            .Where(i => ids.Contains(i.SaleId) && i.DueDate < today && i.AmountPaid < i.Amount)
            .Select(i => new { i.SaleId, i.DueDate, Balance = i.Amount - i.AmountPaid }).ToListAsync(ct);

        return withArrears.Select(s =>
        {
            var mine = overdue.Where(o => o.SaleId == s.Id).ToList();
            var aging = mine.Aggregate(AgingTotals.Empty, (t, o) => t.Add(ArrearsRules.Bucket(ArrearsRules.DaysOverdue(o.DueDate, today)), o.Balance));
            var oldest = mine.Min(o => o.DueDate);
            return new ArrearsSaleRow(s, oldest, ArrearsRules.DaysOverdue(oldest, today), aging);
        }).OrderByDescending(r => r.DaysOverdue).ToList();
    }

    public async Task<IReadOnlyList<SalesCommissionRow>> SalesCommissionAsync(int year, int month, CancellationToken ct = default)
    {
        var (start, end) = BillingRules.MonthBounds(year, month);
        var sales = await ScopedSalesAsync(ct);
        var rows = await db.SalePayments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Active && p.PaymentDate >= start && p.PaymentDate <= end && sales.Any(s => s.Id == p.SaleId)
                        && p.Sale.CommissionPercent > 0)
            .Select(p => new { p.Amount, p.Sale.CommissionPercent, p.Sale.AgentId,
                AgentName = p.Sale.Agent != null ? p.Sale.Agent.FullName : null,
                Share = p.Sale.Agent != null ? p.Sale.Agent.CommissionSharePercent : 0m })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.AgentId)
            .Select(g =>
            {
                var commission = g.Sum(r => SalesRules.Commission(r.Amount, r.CommissionPercent));
                var share = g.First().Share;
                return new SalesCommissionRow(g.Key, g.First().AgentName ?? "No agent assigned", share, g.Sum(r => r.Amount), commission,
                    g.Key is null ? 0 : AgencyRules.AgentShare(commission, share));
            })
            .OrderByDescending(r => r.Commission).ToList();
    }

    // ---------------------------------------------------------------------

    /// <summary>Completed = paid in full AND handed over.</summary>
    private void MaybeComplete(Sale sale)
    {
        var handedOver = sale.Milestones.Any(m => m.Type == MilestoneType.Handover && m.CompletedOn != null);
        if (sale.Status == SaleStatus.Active && sale.Price - sale.AmountPaid <= 0 && handedOver)
        {
            sale.Status = SaleStatus.Completed;
            sale.CompletedOn = Today;
        }
    }

    private static IQueryable<SaleSummary> Project(IQueryable<Sale> query, DateOnly today)
        => query.Select(s => new SaleSummary(s.Id, s.SaleNumber, s.BuyerId, s.Buyer.FullName, s.UnitId, s.Unit.UnitNumber, s.Unit.AssetId, s.Unit.Asset.Name,
            s.Agent != null ? s.Agent.FullName : null, s.SaleDate, s.Price, s.AmountPaid, s.Status,
            s.Status == SaleStatus.Cancelled ? 0 : s.Instalments.Count(i => i.DueDate < today && i.AmountPaid < i.Amount),
            s.Status == SaleStatus.Cancelled ? 0m : s.Instalments.Where(i => i.DueDate < today && i.AmountPaid < i.Amount).Sum(i => (decimal?)(i.Amount - i.AmountPaid)) ?? 0m));
}
