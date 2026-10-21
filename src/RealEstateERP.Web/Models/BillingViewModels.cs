using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

// ------------------------------------------------------------------ charges

public sealed class ChargeFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter a name."), StringLength(100)]
    public string Name { get; set; } = "";

    public ChargeType Type { get; set; } = ChargeType.Recurring;

    [Range(0, 100_000_000)]
    [Display(Name = "Amount (KES)")]
    public decimal? DefaultAmount { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "Price per unit (KES)")]
    public decimal? UnitRate { get; set; }

    [StringLength(20)]
    [Display(Name = "Unit of measure", Prompt = "m³, kWh")]
    public string? UnitOfMeasure { get; set; }

    [Range(0, 100)]
    [Display(Name = "Tax (%)")]
    public decimal TaxPercent { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [Display(Name = "Available for new leases and invoices")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Billed")]
    public ChargeFrequency Frequency { get; set; } = ChargeFrequency.Monthly;

    [Range(1, 12), Display(Name = "Billed in")]
    public int AnchorMonth { get; set; } = 1;

    public ChargeDefinitionInput ToInput() => new(Name, Type, DefaultAmount, UnitRate, UnitOfMeasure, TaxPercent, Description, IsActive, Frequency, AnchorMonth);
}

// ------------------------------------------------------------------ meter readings

public sealed class ReadingsIndexViewModel
{
    public required IReadOnlyList<MeterReadingSummary> Readings { get; init; }
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public required IReadOnlyList<ChargeDefinition> UtilityCharges { get; init; }
    public required bool UnbilledOnly { get; init; }
    public int? AssetId { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class MeterSheetViewModel
{
    public IReadOnlyList<AssetSummary> Assets { get; set; } = [];
    public IReadOnlyList<ChargeDefinition> UtilityCharges { get; set; } = [];
    public IReadOnlyList<MeterSheetRow> Rows { get; set; } = [];
    public int? AssetId { get; set; }
    public int? ChargeId { get; set; }
    public DateTime ReadingDate { get; set; }
    public string? UnitOfMeasure { get; set; }
}

public sealed class MeterSheetPost
{
    public int AssetId { get; set; }
    public int ChargeId { get; set; }
    public DateTime ReadingDate { get; set; }
    public List<MeterSheetPostRow> Rows { get; set; } = [];
}

public sealed class MeterSheetPostRow
{
    public int UnitId { get; set; }
    public decimal? Reading { get; set; }
    public bool IsOpening { get; set; }
}

// ------------------------------------------------------------------ invoices

public sealed class InvoicesIndexViewModel
{
    public required IReadOnlyList<InvoiceSummary> Invoices { get; init; }
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public required BillingSummary Summary { get; init; }
    public required InvoiceListView View { get; init; }
    public string? Month { get; init; }
    public int? AssetId { get; init; }
    public string? Search { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class InvoiceDetailsViewModel
{
    public required InvoiceDetails Details { get; init; }
    public required IReadOnlyList<PaymentSummary> Payments { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAdmin { get; init; }
    public required string BusinessName { get; init; }
}

public sealed class InvoiceFormViewModel
{
    public int? Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public IReadOnlyList<TenantOption> TenantOptions { get; set; } = [];
    public IReadOnlyList<LeaseSummary> LeaseOptions { get; set; } = [];
    public IReadOnlyList<ChargeDefinition> Charges { get; set; } = [];

    [Required(ErrorMessage = "Choose a tenant.")]
    [Display(Name = "Tenant")]
    public int? TenantId { get; set; }

    [Display(Name = "Lease")]
    public int? LeaseId { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Invoice date")]
    public DateTime? InvoiceDate { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Due date")]
    public DateTime? DueDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<InvoiceLineForm> Lines { get; set; } = [];

    public ManualInvoiceInput ToInput() => new(TenantId!.Value, LeaseId, DateOnly.FromDateTime(InvoiceDate!.Value),
        DateOnly.FromDateTime(DueDate!.Value), Notes,
        Lines.Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .Select(l => new InvoiceLineInput(l.LineType, l.Description!, l.Quantity, l.UnitPrice, l.TaxPercent)).ToList());
}

public sealed class InvoiceLineForm
{
    public InvoiceLineType LineType { get; set; } = InvoiceLineType.Other;
    [StringLength(300)] public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TaxPercent { get; set; }
}

public sealed class GenerateInvoicesViewModel
{
    public IReadOnlyList<AssetSummary> Assets { get; set; } = [];
    public IReadOnlyList<BillingPreviewRow> Preview { get; set; } = [];
    public int UnbilledReadings { get; set; }

    [Display(Name = "Month to bill")]
    public string Month { get; set; } = "";
    [Display(Name = "Property")]
    public int? AssetId { get; set; }
    [Display(Name = "Add deposits for new leases")]
    public bool IncludeDeposits { get; set; } = true;
    [Display(Name = "Issue straight away (skip the draft review)")]
    public bool IssueImmediately { get; set; }

    public (int Year, int MonthNumber) Parse()
        => DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", out var d) ? (d.Year, d.Month) : (0, 0);
}

public sealed class LeaseBillingViewModel
{
    public required int LeaseId { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAddCharges { get; init; }
    public required IReadOnlyList<LeaseChargeSummary> Charges { get; init; }
    public required IReadOnlyList<ChargeDefinition> AvailableCharges { get; init; }
    public required IReadOnlyList<InvoiceSummary> Invoices { get; init; }
    public required bool NeedsFinalInvoice { get; init; }
}

public sealed record InvoiceChip(InvoiceStatus Status);
