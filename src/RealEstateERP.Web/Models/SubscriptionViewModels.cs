using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class SubscriptionIndexViewModel
{
    public required SubscriptionSnapshot Snapshot { get; init; }
    public required IReadOnlyList<SubscriptionPayment> Payments { get; init; }
    public required bool CanRenew { get; init; }
    public required DateTime NowUtc { get; init; }
}

public sealed class RenewInput
{
    [Required(ErrorMessage = "Choose a plan.")]
    public int? PlanId { get; set; }

    [Range(1, 12)]
    public int Months { get; set; } = 1;

    [Required(ErrorMessage = "Choose how you paid.")]
    public SubscriptionPaymentMethod? Method { get; set; }

    [Required(ErrorMessage = "Enter the transaction reference.")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "The reference should be 6 to 50 characters.")]
    [Display(Name = "Transaction reference")]
    public string? Reference { get; set; }

    [Display(Name = "Proof of payment")]
    public IFormFile? Proof { get; set; }
}

public sealed class PaymentInstructions
{
    public string MpesaPaybill { get; init; } = "";
    public string MpesaAccount { get; init; } = "";
    public string BankName { get; init; } = "";
    public string BankAccountName { get; init; } = "";
    public string BankAccountNumber { get; init; } = "";
    public string BankBranch { get; init; } = "";
    public string BankSwiftCode { get; init; } = "";
}

public sealed class RenewPageViewModel
{
    public required SubscriptionSnapshot Snapshot { get; init; }
    public required IReadOnlyList<SubscriptionPlan> Plans { get; init; }
    public required PaymentInstructions Instructions { get; init; }
    public SubscriptionPayment? PendingPayment { get; init; }
    public required bool CanRenew { get; init; }
    public required int MaxProofSizeMb { get; init; }
    public RenewInput Input { get; init; } = new();
}

public sealed class PaymentReviewViewModel
{
    public required SubscriptionPayment Payment { get; init; }
    public required SubscriptionSnapshot BusinessSnapshot { get; init; }
}

public sealed class AdminSubscriptionsIndexViewModel
{
    public required IReadOnlyList<SubscriptionPayment> Pending { get; init; }
    public required IReadOnlyList<SubscriptionPayment> RecentlyReviewed { get; init; }
}

public sealed class PlanEditViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(30), RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Letters, numbers and underscores only.")]
    public string Code { get; set; } = "";

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(1, 100_000)]
    [Display(Name = "Unit limit (blank = unlimited)")]
    public int? UnitLimit { get; set; }

    [Range(0, 10_000_000)]
    [Display(Name = "Monthly fee (KES)")]
    public decimal MonthlyFee { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "Credits per month")]
    public int CreditsPerMonth { get; set; }

    [Range(0, 90)]
    [Display(Name = "Trial days")]
    public int TrialDays { get; set; } = 14;

    [Display(Name = "Only for business type")]
    public BusinessType? ForBusinessType { get; set; }

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    [Display(Name = "Available to businesses")]
    public bool IsActive { get; set; } = true;
}
