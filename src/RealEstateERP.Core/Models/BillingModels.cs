using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record ChargeDefinitionInput(
    string Name, ChargeType Type, decimal? DefaultAmount, decimal? UnitRate, string? UnitOfMeasure,
    decimal TaxPercent, string? Description, bool IsActive,
    ChargeFrequency Frequency = ChargeFrequency.Monthly, int AnchorMonth = 1);

public sealed record LeaseChargeInput(int ChargeDefinitionId, decimal? AmountOverride, decimal? UnitRateOverride);

public sealed record LeaseChargeSummary(
    int Id, int ChargeDefinitionId, string Name, ChargeType Type, decimal? Amount, decimal? UnitRate,
    string? UnitOfMeasure, decimal TaxPercent, bool IsOverride);

public sealed record MeterSheetRow(
    int UnitId, string UnitNumber, string? NodePath, string? TenantName,
    decimal? PreviousReading, DateOnly? PreviousDate, decimal Rate, decimal? EnteredReading, bool AlreadyRecorded);

public sealed record MeterReadingEntry(int UnitId, decimal? Reading, bool IsOpening);

public sealed record MeterReadingSummary(
    int Id, int UnitId, string UnitNumber, int AssetId, string AssetName, string ChargeName, string? UnitOfMeasure,
    DateOnly ReadingDate, decimal PreviousReading, decimal CurrentReading, decimal Consumption, decimal UnitRate,
    decimal Amount, bool IsOpening, int? BilledInvoiceId, string? BilledInvoiceNumber);

public sealed record InvoiceLineInput(InvoiceLineType LineType, string Description, decimal Quantity, decimal UnitPrice, decimal TaxPercent);

public sealed record ManualInvoiceInput(
    int TenantId, int? LeaseId, DateOnly InvoiceDate, DateOnly DueDate, string? Notes, IReadOnlyList<InvoiceLineInput> Lines);

public sealed record InvoiceListFilter(InvoiceListView View, DateOnly? Month, int? AssetId, string? Search);

public sealed record InvoiceSummary(
    int Id, string InvoiceNumber, InvoiceKind Kind, int TenantId, string TenantName, int? LeaseId,
    string? UnitNumber, string? AssetName, DateOnly InvoiceDate, DateOnly DueDate, DateOnly? PeriodStart,
    decimal Total, decimal AmountPaid, InvoiceStatus Status)
{
    public decimal Balance => Total - AmountPaid;
}

public sealed record InvoiceDetails(Invoice Invoice, InvoiceStatus Status, IReadOnlyList<InvoiceLine> Lines);

public sealed record BillingRunRequest(int Year, int Month, int? AssetId, bool IncludeDeposits, bool IssueImmediately);

public sealed record BillingPreviewRow(
    int LeaseId, string LeaseNumber, string TenantName, string UnitNumber, string AssetName,
    bool AlreadyInvoiced, int LineCount, decimal EstimatedTotal, IReadOnlyList<PreviewWarning>? Warnings = null);

/// <summary>Something the invoice would miss: an estate charge not on the lease, or no meter reading this month.</summary>
public sealed record PreviewWarning(string Message, int AssetId, int? ChargeDefinitionId, bool IsMissingCharge);

public sealed record BillingRunResult(int Created, int Skipped, decimal TotalInvoiced, IReadOnlyList<string> Problems);

public sealed record BillingSummary(
    decimal InvoicedThisMonth, decimal Outstanding, int OverdueCount, decimal OverdueAmount, int DraftCount, int IssuedCount);
