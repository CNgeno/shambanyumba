using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>KYC documents and signed agreements. Stored privately via IFileStorage.</summary>
public class TenantDocument : TenantEntity
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Set when the document belongs to a specific lease (e.g. the signed agreement).</summary>
    public int? LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public TenantDocumentType DocumentType { get; set; }
    public string? Title { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
