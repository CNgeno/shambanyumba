using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class ArrearsIndexViewModel
{
    public required ArrearsReport Report { get; init; }
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public int? AssetId { get; init; }
    public CollectionStage? Stage { get; init; }
    public required int PenaltiesDue { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class PenaltiesViewModel
{
    public required IReadOnlyList<PenaltyCandidate> Candidates { get; init; }
    public required int DefaultGraceDays { get; init; }
    public required decimal DefaultPercent { get; init; }
}

public sealed class CollectionsPanelModel
{
    public required int TenantId { get; init; }
    public required CollectionStage Stage { get; init; }
    public required IReadOnlyList<CollectionNoteSummary> Notes { get; init; }
    public required IReadOnlyList<TenantDocumentSummary> Documents { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class CollectionNoteForm
{
    public int TenantId { get; set; }
    public CollectionStage Stage { get; set; }
    [Required(ErrorMessage = "Write what happened."), StringLength(1000)]
    public string Note { get; set; } = "";
    [DataType(DataType.Date)]
    public DateTime? PromisedPaymentDate { get; set; }
    public int? DocumentId { get; set; }
}

public sealed class CreditsViewModel
{
    public required CreditOverview Overview { get; init; }
    public required PaymentInstructions Instructions { get; init; }
    public required bool CanBuy { get; init; }
}

public sealed class BuyCreditsForm
{
    public int Credits { get; set; }
    public SubscriptionPaymentMethod Method { get; set; } = SubscriptionPaymentMethod.Mpesa;
    [Required(ErrorMessage = "Enter the transaction reference."), StringLength(50, MinimumLength = 6)]
    public string Reference { get; set; } = "";
}

public sealed class AdminCreditsViewModel
{
    public required IReadOnlyList<CreditPurchase> Pending { get; init; }
}
