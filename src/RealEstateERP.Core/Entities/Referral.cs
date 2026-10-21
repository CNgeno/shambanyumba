using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>Someone who brings customers: an influencer, a business developer, or a customer (ReferrerBusinessId). Platform-level.</summary>
public class Partner : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Personal code, upper case; the link is shambanyumba.com/?ref=CODE.</summary>
    public string Code { get; set; } = string.Empty;
    public PartnerKind Kind { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>Paid per referred business when it makes its first subscription payment (0 = none, e.g. customers, who get a free month instead).</summary>
    public decimal CommissionPerCustomer { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    /// <summary>Set for customer referrers: their business gets 1 month free per referred business that pays.</summary>
    public int? ReferrerBusinessId { get; set; }
}

/// <summary>Commission earned by a partner for one referred business (created at that business's first payment).</summary>
public class PartnerCommission : AuditableEntity
{
    public int PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public int BusinessId { get; set; }
    public decimal Amount { get; set; }
    public CommissionStatus Status { get; set; } = CommissionStatus.Owed;
    public DateTime? PaidAtUtc { get; set; }
    public string? PaidReference { get; set; }
}
