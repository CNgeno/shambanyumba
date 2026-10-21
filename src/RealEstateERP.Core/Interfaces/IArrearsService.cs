using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>FR-09. Aging report, late penalties and collections notes. Depends on: DbContext, INumberSequenceService, ISystemConfigService, TimeProvider.</summary>
public interface IArrearsService
{
    Task<ArrearsReport> GetReportAsync(int? assetId, CancellationToken ct = default);
    Task<IReadOnlyList<PenaltyCandidate>> PreviewPenaltiesAsync(CancellationToken ct = default);
    Task<PenaltyRunResult> ApplyPenaltiesAsync(IReadOnlyCollection<int>? onlyInvoiceIds, CancellationToken ct = default);

    Task<IReadOnlyList<CollectionNoteSummary>> GetNotesAsync(int tenantId, CancellationToken ct = default);
    Task<Result> AddNoteAsync(CollectionNoteInput input, int? documentId, CancellationToken ct = default);
}

/// <summary>
/// FR-15 credits. LOW-LEVEL: depends on DbContext, ICurrentUserService, ISystemConfigService only.
/// TryConsumeAsync saves on its own, so call it before adding other changes.
/// </summary>
public interface ICreditService
{
    Task<int> GetBalanceAsync(CancellationToken ct = default);
    Task<CreditOverview> GetOverviewAsync(CancellationToken ct = default);
    Task<Result> TryConsumeAsync(int credits, string description, string? reference, CancellationToken ct = default);
    Task<Result<int>> RequestPurchaseAsync(CreditPurchaseInput input, CancellationToken ct = default);

    // Platform admin
    Task<IReadOnlyList<CreditPurchase>> GetPendingPurchasesAsync(CancellationToken ct = default);
    Task<Result> ApprovePurchaseAsync(int purchaseId, string reviewerUserId, CancellationToken ct = default);
    Task<Result> RejectPurchaseAsync(int purchaseId, string reviewerUserId, string reason, CancellationToken ct = default);
    Task<Result> AdjustAsync(int businessId, int credits, string reason, CancellationToken ct = default);
}

/// <summary>PDF documents (QuestPDF). Pure rendering: no database.</summary>
public interface IPdfService
{
    byte[] Invoice(InvoiceDetails invoice, BusinessProfile business);
    byte[] Receipt(PaymentDetails payment, BusinessProfile business);
    byte[] Statement(TenantStatement statement, BusinessProfile business);
    byte[] Remittance(RemittanceDetails remittance, BusinessProfile business);
    byte[] SaleStatement(SaleDetails sale, BusinessProfile business);
}
