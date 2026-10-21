using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-10. A landlord whose properties an agency manages (the agency's client).</summary>
public class PropertyOwner : TenantEntity
{
    public OwnerType Type { get; set; } = OwnerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string? IdNumber { get; set; }
    public string? KraPin { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PostalAddress { get; set; }

    // Where the owner's money is sent
    public PayoutMethod PayoutMethod { get; set; } = PayoutMethod.Bank;
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? MpesaNumber { get; set; }

    public string? Notes { get; set; }
}
