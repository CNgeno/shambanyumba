using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-05-B. A charge attached to a lease, optionally at a different price than the default.</summary>
public class LeaseCharge : TenantEntity
{
    public int LeaseId { get; set; }
    public Lease Lease { get; set; } = null!;

    public int ChargeDefinitionId { get; set; }
    public ChargeDefinition ChargeDefinition { get; set; } = null!;

    public decimal? AmountOverride { get; set; }
    public decimal? UnitRateOverride { get; set; }
}
