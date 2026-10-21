using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum AssetType
{
    Land,
    Building,
    Estate,
    Mall,
    [Display(Name = "Commercial complex")] CommercialComplex
}

public enum AssetLifecycle
{
    Planned,
    [Display(Name = "Under construction")] UnderConstruction,
    Operational,
    [Display(Name = "For sale")] ForSale,
    Inactive
}

public enum StructureNodeType
{
    Block,
    Building,
    Wing,
    Floor
}

public enum UnitType
{
    Apartment,
    Bedsitter,
    Shop,
    Office,
    Plot,
    Warehouse,
    Other
}

/// <summary>FR-03-D. Occupied is only ever set by a lease (Sprint 3), never by hand.</summary>
public enum UnitStatus
{
    Available,
    Reserved,
    Occupied,
    Sold,
    Inactive
}
