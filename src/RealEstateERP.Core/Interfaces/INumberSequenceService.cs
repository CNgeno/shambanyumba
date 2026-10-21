namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// LOW-LEVEL. Per-business running numbers. NextAsync saves immediately, so call it BEFORE adding
/// other entities to the context (it refuses to run if there are unsaved changes).
/// Numbers are never reused; a failed operation after NextAsync leaves a gap.
/// </summary>
public interface INumberSequenceService
{
    Task<long> NextAsync(string key, CancellationToken ct = default);
}

public static class SequenceKeys
{
    public const string Lease = "LEASE";
    public const string Invoice = "INVOICE";
    public const string Receipt = "RECEIPT";
    public const string Refund = "REFUND";
    public const string Contract = "CONTRACT";
    public const string Remittance = "REMITTANCE";
    public const string Sale = "SALE";
    public const string SaleReceipt = "SALE_RECEIPT";
}
