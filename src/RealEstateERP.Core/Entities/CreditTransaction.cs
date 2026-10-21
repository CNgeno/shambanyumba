using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-15 credit ledger. Platform-level (like Subscription): written by plan grants and admin approvals as well as by
/// the business itself, so it is NOT an ITenantEntity and is always queried by BusinessId explicitly.
/// BalanceAfter gives the running balance; Business.CreditBalance always equals the latest BalanceAfter.
/// </summary>
public class CreditTransaction : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public CreditTransactionType Type { get; set; }
    public int Credits { get; set; }        // + added, - used
    public int BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
}
