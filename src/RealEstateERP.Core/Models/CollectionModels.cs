using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record PaymentInput(
    int TenantId,
    int? LeaseId,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? PayerName,
    string? Notes,
    AllocationMode Mode,
    IReadOnlyDictionary<int, decimal>? ManualAllocations);

public sealed record RefundInput(DateOnly RefundDate, decimal Amount, PaymentMethod Method, string? Reference, string? Notes);

public sealed record PaymentListFilter(DateOnly? Month, PaymentMethod? Method, string? Search, bool IncludeReversed);

public sealed record PaymentSummary(
    int Id, string ReceiptNumber, int TenantId, string TenantName, DateOnly PaymentDate, decimal Amount,
    PaymentMethod Method, string? Reference, decimal Allocated, decimal Refunded, PaymentStatus Status)
{
    public decimal Unallocated => Status == PaymentStatus.Active ? Amount - Allocated - Refunded : 0;
}

public sealed record OpenInvoice(
    int Id, string InvoiceNumber, DateOnly InvoiceDate, DateOnly DueDate, DateOnly? PeriodStart,
    string? UnitNumber, decimal Total, decimal AmountPaid)
{
    public decimal Balance => Total - AmountPaid;
}

public sealed record AllocationLine(int Id, int InvoiceId, string InvoiceNumber, DateOnly DueDate, decimal Amount, bool IsReversed);

public sealed record RefundSummary(
    int Id, string RefundNumber, RefundKind Kind, int TenantId, string TenantName, int? LeaseId, string? LeaseNumber,
    int? PaymentId, string? ReceiptNumber, DateOnly RefundDate, decimal Amount, PaymentMethod Method, string? Reference, string? Notes);

public sealed record PaymentDetails(
    Payment Payment,
    IReadOnlyList<AllocationLine> Allocations,
    IReadOnlyList<RefundSummary> Refunds,
    decimal Unallocated,
    TenantBalance TenantBalance);

public sealed record TenantBalance(decimal Outstanding, decimal Credit, DateOnly? LastPaymentDate, int OpenInvoiceCount)
{
    /// <summary>Positive: the tenant owes. Negative: the tenant is in credit.</summary>
    public decimal Net => Outstanding - Credit;
}

public sealed record CollectionSummary(decimal CollectedThisMonth, int PaymentsThisMonth, decimal UnallocatedCredit, decimal RefundedThisMonth);

public sealed record StatementEntry(
    DateOnly Date, StatementEntryType Type, string Reference, string Description, decimal Debit, decimal Credit,
    decimal Balance, string? LinkController, int? LinkId);

public sealed record TenantStatement(
    Tenant Tenant, DateOnly? From, DateOnly To, decimal OpeningBalance, IReadOnlyList<StatementEntry> Entries, decimal ClosingBalance)
{
    public decimal TotalDebits => Entries.Sum(e => e.Debit);
    public decimal TotalCredits => Entries.Sum(e => e.Credit);
}
