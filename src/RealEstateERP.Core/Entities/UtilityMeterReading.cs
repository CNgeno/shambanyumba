using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-06-B. A meter belongs to the UNIT, not the lease. Consumption and amount are calculated when the
/// reading is saved, with the rate in force at that moment (snapshot).
/// BilledInvoiceId is set when an invoice picks it up; a reading can be billed only once.
/// </summary>
public class UtilityMeterReading : TenantEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int ChargeDefinitionId { get; set; }
    public ChargeDefinition ChargeDefinition { get; set; } = null!;

    public DateOnly ReadingDate { get; set; }
    public decimal PreviousReading { get; set; }
    public decimal CurrentReading { get; set; }
    public decimal Consumption { get; set; }
    public decimal UnitRate { get; set; }
    public decimal Amount { get; set; }

    /// <summary>The first reading on a meter: a starting point, never billed.</summary>
    public bool IsOpening { get; set; }

    public int? BilledInvoiceId { get; set; }
    public Invoice? BilledInvoice { get; set; }

    public string? Notes { get; set; }
}
