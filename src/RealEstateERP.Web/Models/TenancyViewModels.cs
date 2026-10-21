using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

// ------------------------------------------------------------------ tenants

public sealed class TenantsIndexViewModel
{
    public required IReadOnlyList<TenantSummary> Tenants { get; init; }
    public required IReadOnlyDictionary<int, TenantBalance> Balances { get; init; }
    public bool Owing { get; init; }
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public TenantStatus? Status { get; init; }
    public int? AssetId { get; init; }
    public string? Search { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class TenantFormViewModel
{
    public int? Id { get; set; }

    public TenantType Type { get; set; } = TenantType.Individual;

    [Required(ErrorMessage = "Enter the tenant's name."), StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Display(Name = "ID type")]
    public IdDocumentType? IdType { get; set; } = IdDocumentType.NationalId;

    [StringLength(50)]
    [Display(Name = "ID / registration number")]
    public string? IdNumber { get; set; }

    [StringLength(20)]
    [Display(Name = "KRA PIN")]
    public string? KraPin { get; set; }

    [StringLength(20)]
    [Display(Name = "Phone", Prompt = "0712 345 678")]
    public string? Phone { get; set; }

    [StringLength(20)]
    [Display(Name = "Alternative phone")]
    public string? AltPhone { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(300)]
    [Display(Name = "Postal address")]
    public string? PostalAddress { get; set; }

    [StringLength(150)]
    [Display(Name = "Employer / occupation")]
    public string? Employer { get; set; }

    [StringLength(150)]
    [Display(Name = "Name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    [Display(Name = "Phone")]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(50)]
    [Display(Name = "Relationship")]
    public string? EmergencyContactRelationship { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>After creating, go straight to a new lease for this unit.</summary>
    public int? ForUnitId { get; set; }

    public TenantInput ToInput() => new(Type, FullName, IdType, IdNumber, KraPin, Phone, AltPhone, Email, PostalAddress,
        Employer, EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship, Notes);
}

public sealed class TenantDetailsViewModel
{
    public required TenantDetails Details { get; init; }
    public required TenantBalance Balance { get; init; }
    public required CollectionsPanelModel Collections { get; init; }
    public required IReadOnlyList<PaymentSummary> RecentPayments { get; init; }
    public required bool CanEdit { get; init; }
    public required int MaxUploadMb { get; init; }
}

// ------------------------------------------------------------------ leases

public sealed class LeasesIndexViewModel
{
    public required IReadOnlyList<LeaseSummary> Leases { get; init; }
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public required LeaseCounts Counts { get; init; }
    public required LeaseListView View { get; init; }
    public int? AssetId { get; init; }
    public string? Search { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class LeaseFormViewModel
{
    public int? Id { get; set; }
    public string? LeaseNumber { get; set; }
    public IReadOnlyList<TenantOption> TenantOptions { get; set; } = [];
    public IReadOnlyList<LettableUnit> UnitOptions { get; set; } = [];

    [Required(ErrorMessage = "Choose a tenant.")]
    [Display(Name = "Tenant")]
    public int? TenantId { get; set; }

    [Required(ErrorMessage = "Choose a unit.")]
    [Display(Name = "Unit")]
    public int? UnitId { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime? EndDate { get; set; }

    [Range(1, 100_000_000, ErrorMessage = "Enter the monthly rent.")]
    [Display(Name = "Monthly rent (KES)")]
    public decimal MonthlyRent { get; set; }

    [Range(0, 100_000_000)]
    [Display(Name = "Deposit (KES)")]
    public decimal DepositAmount { get; set; }

    [Range(0, 365)]
    [Display(Name = "Notice period (days)")]
    public int NoticePeriodDays { get; set; } = 30;

    [Range(1, 28)]
    [Display(Name = "Rent due on day")]
    public int RentDueDay { get; set; } = 1;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public LeaseInput ToInput() => new(TenantId!.Value, UnitId!.Value, DateOnly.FromDateTime(StartDate!.Value),
        EndDate is DateTime e ? DateOnly.FromDateTime(e) : null, MonthlyRent, DepositAmount, NoticePeriodDays, RentDueDay, Notes);
}

public sealed class LeaseDetailsViewModel
{
    public required LeaseDetails Details { get; init; }
    public required LeaseBillingViewModel Billing { get; init; }
    public required decimal DepositRefundable { get; init; }
    public required DateOnly Today { get; init; }
    public required bool CanEdit { get; init; }
    public required int MaxUploadMb { get; init; }
}

public sealed class LeaseRenewViewModel
{
    public int LeaseId { get; set; }
    public LeaseSummary? Current { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "New start date")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "New end date")]
    public DateTime? EndDate { get; set; }

    [Range(1, 100_000_000, ErrorMessage = "Enter the monthly rent.")]
    [Display(Name = "Monthly rent (KES)")]
    public decimal MonthlyRent { get; set; }

    [Range(0, 100_000_000)]
    [Display(Name = "Deposit held (KES)")]
    public decimal DepositAmount { get; set; }

    [Range(0, 365)]
    [Display(Name = "Notice period (days)")]
    public int NoticePeriodDays { get; set; }

    [Range(1, 28)]
    [Display(Name = "Rent due on day")]
    public int RentDueDay { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public LeaseRenewalInput ToInput() => new(DateOnly.FromDateTime(StartDate!.Value),
        EndDate is DateTime e ? DateOnly.FromDateTime(e) : null, MonthlyRent, DepositAmount, NoticePeriodDays, RentDueDay, Notes);
}

public sealed class LeaseTerminateViewModel
{
    public int LeaseId { get; set; }
    public LeaseSummary? Current { get; set; }
    public decimal DepositHeld { get; set; }
    public int NoticePeriodDays { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Tenant leaves on")]
    public DateTime? EndDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Notice given on")]
    public DateTime? NoticeGivenOn { get; set; }

    [Required(ErrorMessage = "Give a reason."), StringLength(500)]
    public string Reason { get; set; } = "";

    [Range(0, 100_000_000)]
    [Display(Name = "Early termination penalty (KES)")]
    public decimal Penalty { get; set; }

    [Range(0, 100_000_000)]
    [Display(Name = "Deductions from deposit (KES)")]
    public decimal DepositDeductions { get; set; }

    [StringLength(500)]
    [Display(Name = "What the deductions are for")]
    public string? DepositNote { get; set; }

    [Display(Name = "Make the unit inactive (e.g. for renovation) instead of available")]
    public bool MakeUnitInactive { get; set; }

    public LeaseTerminationInput ToInput() => new(DateOnly.FromDateTime(EndDate!.Value),
        NoticeGivenOn is DateTime n ? DateOnly.FromDateTime(n) : null, Reason, Penalty, DepositDeductions, DepositNote, MakeUnitInactive);
}

public sealed class DocumentUploadInput
{
    public TenantDocumentType DocumentType { get; set; } = TenantDocumentType.NationalId;
    [StringLength(150)] public string? Title { get; set; }
    public int? LeaseId { get; set; }
    public IFormFile? File { get; set; }
}

// ------------------------------------------------------------------ partial models

public sealed record LeaseChip(LeaseStatus Status, LeaseExpiry Expiry);
public sealed record DocumentListModel(IReadOnlyList<TenantDocumentSummary> Documents, bool CanEdit, string ReturnUrl);
public sealed record DocumentUploadModel(int TenantId, int? LeaseId, int MaxMb, string ReturnUrl, TenantDocumentType DefaultType);
