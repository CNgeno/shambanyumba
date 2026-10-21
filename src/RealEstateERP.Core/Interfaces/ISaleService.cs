using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// FR-11. Buyers, sales, schedules, buyer payments, transfer milestones and reports.
/// Depends on: DbContext, INumberSequenceService, IAgentScopeService, IFileStorage, ISystemConfigService, ICurrentUserService, TimeProvider.
/// The only code that sets a unit to Reserved/Sold for a sale, and the only writer of Sale/SaleInstalment.AmountPaid.
/// </summary>
public interface ISaleService
{
    // Buyers
    Task<IReadOnlyList<BuyerSummary>> ListBuyersAsync(string? search, CancellationToken ct = default);
    Task<Buyer?> GetBuyerAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveBuyerAsync(int? id, BuyerInput input, CancellationToken ct = default);

    // Sales
    Task<IReadOnlyList<SaleSummary>> ListAsync(SalesListView view, int? buyerId, int? assetId, string? search, CancellationToken ct = default);
    Task<SaleDetails?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SellableUnit>> GetSellableUnitsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleLine>> PreviewScheduleAsync(SaleInput input, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(SaleInput input, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, string reason, bool makeUnitInactive, CancellationToken ct = default);

    // Payments
    Task<Result<int>> RecordPaymentAsync(int saleId, SalePaymentInput input, CancellationToken ct = default);
    Task<Result> ReversePaymentAsync(int salePaymentId, string reason, CancellationToken ct = default);

    // Milestones
    Task<Result> CompleteMilestoneAsync(int saleId, MilestoneType type, DateOnly completedOn, string? reference, string? notes, UploadedFile? document, CancellationToken ct = default);
    Task<SaleMilestone?> GetMilestoneAsync(int milestoneId, CancellationToken ct = default);

    // Reports
    Task<SalesSummaryTotals> GetTotalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventoryRow>> GetInventoryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ArrearsSaleRow>> GetInstalmentArrearsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SalesCommissionRow>> SalesCommissionAsync(int year, int month, CancellationToken ct = default);
}
