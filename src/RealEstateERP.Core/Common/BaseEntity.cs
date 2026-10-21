namespace RealEstateERP.Core.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }
}

public abstract class AuditableEntity : BaseEntity, IAuditable, ISoftDelete
{
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>SQL Server rowversion: optimistic concurrency on every domain table.</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>Base for everything that belongs to a business (assets, units, leases, invoices...).</summary>
public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public int BusinessId { get; set; }
}
