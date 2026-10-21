using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>Per-business running numbers (leases now; invoices and receipts later).</summary>
public class NumberSequence : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public long NextValue { get; set; } = 1;
}
