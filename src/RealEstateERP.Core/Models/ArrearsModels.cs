using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record AgingTotals(decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90)
{
    public static AgingTotals Empty { get; } = new(0, 0, 0, 0);
    public decimal Total => Days1To30 + Days31To60 + Days61To90 + Over90;

    public AgingTotals Add(AgingBucket bucket, decimal amount) => bucket switch
    {
        AgingBucket.Days1To30 => this with { Days1To30 = Days1To30 + amount },
        AgingBucket.Days31To60 => this with { Days31To60 = Days31To60 + amount },
        AgingBucket.Days61To90 => this with { Days61To90 = Days61To90 + amount },
        _ => this with { Over90 = Over90 + amount }
    };
}

public sealed record PropertyArrears(int AssetId, string AssetName, AgingTotals Aging, int TenantCount);

public sealed record TenantArrears(
    int TenantId, string TenantName, string? Phone, string? Units, CollectionStage Stage,
    int OverdueInvoices, int OldestDaysOverdue, AgingTotals Aging, decimal Credit, DateOnly? LastPaymentDate);

public sealed record ArrearsReport(
    AgingTotals Totals, decimal NotYetDue, IReadOnlyList<PropertyArrears> ByProperty, IReadOnlyList<TenantArrears> Tenants);

public sealed record PenaltyCandidate(
    int InvoiceId, string InvoiceNumber, int TenantId, string TenantName, string? UnitNumber,
    DateOnly DueDate, int DaysOverdue, int GraceDays, decimal Balance, decimal Penalty, bool IsFixed);

public sealed record PenaltyRunResult(int Created, decimal Total, IReadOnlyList<string> Problems);

public sealed record CollectionNoteInput(int TenantId, CollectionStage Stage, string Note, DateOnly? PromisedPaymentDate);

public sealed record CollectionNoteSummary(
    int Id, DateTime CreatedAtUtc, string? CreatedBy, CollectionStage Stage, bool StageChanged, string Note,
    DateOnly? PromisedPaymentDate, int? DocumentId, string? DocumentName);

public sealed record CreditOverview(int Balance, IReadOnlyList<CreditTransaction> History, IReadOnlyList<CreditPurchase> Purchases,
    decimal PricePerCredit, IReadOnlyList<int> Bundles);

public sealed record CreditPurchaseInput(int Credits, SubscriptionPaymentMethod Method, string Reference);

public sealed record BusinessProfile(string Name, string? Phone, string? Email, string? KraPin, string? Address)
{
    /// <summary>Storage key of the uploaded logo, if any.</summary>
    public string? LogoKey { get; init; }

    /// <summary>Logo file contents, loaded by the caller when a document needs it (PDFs).</summary>
    public byte[]? Logo { get; init; }

    public BusinessType Type { get; init; }
    public bool? ShowRentalTools { get; init; }

    /// <summary>Whether the rental menus show: the business's choice, else on for everyone except developers.</summary>
    public bool RentalToolsEnabled => Common.RentalTools.Enabled(Type, ShowRentalTools);
}
