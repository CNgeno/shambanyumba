using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-09-C. A dated note in a tenant's collections history (calls, promises to pay, demand letters, stage changes).</summary>
public class CollectionNote : TenantEntity
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public CollectionStage Stage { get; set; }
    public bool StageChanged { get; set; }
    public string Note { get; set; } = string.Empty;

    /// <summary>Optional supporting document (e.g. the demand letter), stored with the tenant's documents.</summary>
    public int? DocumentId { get; set; }
    public TenantDocument? Document { get; set; }

    public DateOnly? PromisedPaymentDate { get; set; }
}
