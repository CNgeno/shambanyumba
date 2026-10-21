using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-12. A unit advertised to let or for sale on the public Properties pages.
/// Shown publicly only while Published AND the unit is still Available. One open listing per unit and type.
/// </summary>
public class Listing : TenantEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public ListingType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Monthly rent (Rent) or asking price (Sale).</summary>
    public decimal Price { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int ViewCount { get; set; }

    public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();
}

public class ListingPhoto : TenantEntity
{
    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;

    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
