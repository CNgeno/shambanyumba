using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class PaymentsIndexViewModel
{
    public required IReadOnlyList<PaymentSummary> Payments { get; init; }
    public required IReadOnlyList<RefundSummary> Refunds { get; init; }
    public required CollectionSummary Summary { get; init; }
    public required bool ShowRefunds { get; init; }
    public string? Month { get; init; }
    public PaymentMethod? Method { get; init; }
    public string? Search { get; init; }
    public bool IncludeReversed { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class RecordPaymentViewModel
{
    public IReadOnlyList<TenantOption> TenantOptions { get; set; } = [];
    public IReadOnlyList<OpenInvoice> OpenInvoices { get; set; } = [];
    public IReadOnlyList<LeaseSummary> LeaseOptions { get; set; } = [];
    public TenantBalance? Balance { get; set; }
    public string? TenantName { get; set; }

    [Required(ErrorMessage = "Choose a tenant.")]
    [Display(Name = "Tenant")]
    public int? TenantId { get; set; }

    [Display(Name = "Lease")]
    public int? LeaseId { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date received")]
    public DateTime? PaymentDate { get; set; }

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount received.")]
    [Display(Name = "Amount (KES)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Mpesa;

    [StringLength(50)]
    [Display(Name = "Reference")]
    public string? Reference { get; set; }

    [StringLength(150)]
    [Display(Name = "Paid by (if not the tenant)")]
    public string? PayerName { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public AllocationMode Mode { get; set; } = AllocationMode.OldestFirst;

    /// <summary>Manual mode: invoice id -> amount.</summary>
    public Dictionary<int, decimal?> Allocations { get; set; } = [];

    public PaymentInput ToInput() => new(TenantId!.Value, LeaseId, DateOnly.FromDateTime(PaymentDate!.Value), Amount, Method,
        Reference, PayerName, Notes, Mode,
        Allocations.Where(kv => kv.Value is > 0).ToDictionary(kv => kv.Key, kv => kv.Value!.Value));
}

public sealed class PaymentDetailsViewModel
{
    public required PaymentDetails Details { get; init; }
    public required string BusinessName { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAdmin { get; init; }
}

public sealed class RefundViewModel
{
    public RefundKind Kind { get; set; }
    public int? PaymentId { get; set; }
    public int? LeaseId { get; set; }
    public string TenantName { get; set; } = "";
    public string Context { get; set; } = "";
    public decimal Available { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date refunded")]
    public DateTime? RefundDate { get; set; }

    [Range(0.01, 100_000_000, ErrorMessage = "Enter the amount refunded.")]
    [Display(Name = "Amount (KES)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Mpesa;

    [StringLength(50)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public RefundInput ToInput() => new(DateOnly.FromDateTime(RefundDate!.Value), Amount, Method, Reference, Notes);
}

public sealed class TenantStatementViewModel
{
    public required TenantStatement Statement { get; init; }
    public required TenantBalance Balance { get; init; }
    public required string BusinessName { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public sealed record BalanceCell(TenantBalance? Balance);
