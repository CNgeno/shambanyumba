using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-04-A. A person or company renting from the business.</summary>
public class Tenant : TenantEntity
{
    public TenantType Type { get; set; } = TenantType.Individual;
    public string FullName { get; set; } = string.Empty;

    public IdDocumentType? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? KraPin { get; set; }

    /// <summary>Normalised to +2547XXXXXXXX (see KenyanPhone) so SMS works later.</summary>
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? PostalAddress { get; set; }
    public string? Employer { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }

    public string? Notes { get; set; }

    /// <summary>FR-09-C collections stage. Every change is logged as a CollectionNote.</summary>
    public CollectionStage CollectionStage { get; set; } = CollectionStage.Normal;

    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<TenantDocument> Documents { get; set; } = new List<TenantDocument>();
}
