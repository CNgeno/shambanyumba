using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-11-A. Someone buying a unit or plot (kept separate from tenants).</summary>
public class Buyer : TenantEntity
{
    public OwnerType Type { get; set; } = OwnerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string? IdNumber { get; set; }
    public string? KraPin { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PostalAddress { get; set; }
    public string? Notes { get; set; }
}
