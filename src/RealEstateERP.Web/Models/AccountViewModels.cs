using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class BusinessProfileForm
{
    [Required(ErrorMessage = "Enter the business name."), StringLength(200)]
    [Display(Name = "Business name")]
    public string Name { get; set; } = "";

    [Display(Name = "We manage properties for other owners (agency)")]
    public bool ManagesForOthers { get; set; }

    public BusinessType Type => ManagesForOthers ? BusinessType.Agency : BusinessType.Owner;

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(500)]
    [Display(Name = "Physical address")]
    public string? PhysicalAddress { get; set; }

    [RegularExpression(@"^[A-Za-z]\d{9}[A-Za-z]$", ErrorMessage = "A KRA PIN looks like A123456789Z.")]
    [Display(Name = "KRA PIN")]
    public string? KraPin { get; set; }

    public bool HasLogo { get; set; }

    [Display(Name = "We also let units (show tenants, leases, invoices, payments, arrears and rent reports)")]
    public bool ShowRentalTools { get; set; }

    public BusinessProfileInput ToInput() => new(Name, Type, Email, Phone, PhysicalAddress, KraPin, ShowRentalTools);
}

public sealed class TeamViewModel
{
    public required IReadOnlyList<TeamMember> Members { get; init; }
    public required IReadOnlyList<InvitationSummary> Invitations { get; init; }
    public required bool IsOwner { get; init; }
    public BusinessType? BusinessType { get; init; }
    public string Label(BusinessRole role) => RealEstateERP.Core.Common.RoleLabels.For(role, BusinessType);
}

public sealed class InvitationPageViewModel
{
    public required string Token { get; init; }
    public InvitationLinkInfo? Info { get; init; }
    public required bool SignedIn { get; init; }
    public string? SignedInEmail { get; init; }
}

public sealed class SelectBusinessViewModel
{
    public required IReadOnlyList<UserBusinessSummary> Businesses { get; init; }
    public required IReadOnlyList<PendingInvitation> Invitations { get; init; }
}

public sealed class AdminUsersViewModel
{
    public required IReadOnlyList<AdminUserSummary> Users { get; init; }
    public string? Search { get; init; }
    public required string CurrentUserId { get; init; }
}

public sealed record AdminBusinessRow(AdminBusinessSummary Business, SubscriptionSnapshot Subscription);

public sealed class AdminBusinessesViewModel
{
    public required IReadOnlyList<AdminBusinessRow> Businesses { get; init; }
    public string? Search { get; init; }
}

public sealed class DevMailboxViewModel
{
    public required IReadOnlyList<DevEmail> Emails { get; init; }
    public DevEmail? Selected { get; init; }
}

public sealed class AutomationForm
{
    public bool AutoInvoiceEnabled { get; set; }
    [Range(1, 28)] public int AutoInvoiceDay { get; set; } = 1;
    public bool AutoPenaltiesEnabled { get; set; }
    [Range(0, 60)] public int? PenaltyGraceDays { get; set; }
    [Range(0, 50)] public decimal? PenaltyPercent { get; set; }
    public bool EmailInvoicesToTenants { get; set; }
    public bool EmailReceiptsToTenants { get; set; }
    [StringLength(1000)] public string? PaymentInstructions { get; set; }
    public bool AutoListVacantUnits { get; set; }

    public RealEstateERP.Core.Models.AutomationSettings ToSettings() => new(AutoInvoiceEnabled, AutoInvoiceDay, AutoPenaltiesEnabled, PenaltyGraceDays,
        PenaltyPercent, EmailInvoicesToTenants, EmailReceiptsToTenants, PaymentInstructions, AutoListVacantUnits);
}

public sealed class AutomationViewModel
{
    public required AutomationForm Form { get; init; }
    public required RealEstateERP.Core.Models.AutomationStatus Status { get; init; }
}
