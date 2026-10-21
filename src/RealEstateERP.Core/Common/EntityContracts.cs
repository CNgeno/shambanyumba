namespace RealEstateERP.Core.Common;

/// <summary>Row belongs to exactly one business. Gets the tenant query filter automatically.</summary>
public interface ITenantEntity
{
    int BusinessId { get; set; }
}

/// <summary>Row is never physically deleted (NFR-03). Gets the soft-delete query filter automatically.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

/// <summary>Created/updated stamps are filled in by the DbContext, never by services.</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedBy { get; set; }
}
