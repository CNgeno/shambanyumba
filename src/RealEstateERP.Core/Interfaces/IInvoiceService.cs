using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Invoices: queries, manual invoices and lifecycle. Depends on: DbContext, INumberSequenceService, TimeProvider.</summary>
public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceSummary>> ListAsync(InvoiceListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceSummary>> ListForLeaseAsync(int leaseId, CancellationToken ct = default);
    Task<InvoiceDetails?> GetDetailsAsync(int invoiceId, CancellationToken ct = default);
    Task<BillingSummary> GetSummaryAsync(CancellationToken ct = default);

    Task<Result<int>> CreateManualAsync(ManualInvoiceInput input, bool issue, CancellationToken ct = default);
    Task<Result> UpdateDraftAsync(int invoiceId, ManualInvoiceInput input, CancellationToken ct = default);
    Task<Result<int>> CreateFinalInvoiceAsync(int leaseId, CancellationToken ct = default);

    Task<Result> IssueAsync(int invoiceId, CancellationToken ct = default);
    Task<Result<int>> IssueAllDraftsAsync(CancellationToken ct = default);
    Task<Result> DeleteDraftAsync(int invoiceId, CancellationToken ct = default);
    Task<Result> CancelAsync(int invoiceId, string reason, CancellationToken ct = default);
    Task<Result> WriteOffAsync(int invoiceId, string reason, CancellationToken ct = default);
}

/// <summary>FR-07-A monthly generation. Depends on: DbContext, INumberSequenceService, TimeProvider.</summary>
public interface IBillingRunService
{
    Task<IReadOnlyList<BillingPreviewRow>> PreviewAsync(BillingRunRequest request, CancellationToken ct = default);
    Task<BillingRunResult> GenerateAsync(BillingRunRequest request, CancellationToken ct = default);
}
