using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// Payments, allocations and refunds. THE ONLY code that changes Invoice.AmountPaid.
/// Depends on: DbContext, INumberSequenceService, ICurrentUserService, TimeProvider.
/// </summary>
public interface IPaymentService
{
    Task<IReadOnlyList<PaymentSummary>> ListAsync(PaymentListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentSummary>> ListForInvoiceAsync(int invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<RefundSummary>> ListRefundsAsync(DateOnly? month, CancellationToken ct = default);
    Task<PaymentDetails?> GetDetailsAsync(int paymentId, CancellationToken ct = default);
    Task<IReadOnlyList<OpenInvoice>> GetOpenInvoicesAsync(int tenantId, CancellationToken ct = default);
    Task<TenantBalance> GetTenantBalanceAsync(int tenantId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, TenantBalance>> GetTenantBalancesAsync(IReadOnlyCollection<int> tenantIds, CancellationToken ct = default);
    Task<CollectionSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<decimal> GetDepositRefundableAsync(int leaseId, CancellationToken ct = default);

    Task<Result<int>> RecordAsync(PaymentInput input, CancellationToken ct = default);
    Task<Result<decimal>> ApplyCreditAsync(int tenantId, CancellationToken ct = default);
    Task<Result> ReverseAsync(int paymentId, string reason, CancellationToken ct = default);
    Task<Result<int>> RefundOverpaymentAsync(int paymentId, RefundInput input, CancellationToken ct = default);
    Task<Result<int>> RefundDepositAsync(int leaseId, RefundInput input, CancellationToken ct = default);
}

/// <summary>FR-04-C. Read-only: built from invoices, payments, reversals and refunds. Depends on: DbContext.</summary>
public interface IStatementService
{
    Task<TenantStatement?> GetStatementAsync(int tenantId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
