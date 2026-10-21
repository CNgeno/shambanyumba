using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

/// <summary>ActionUrl null = not available yet (later sprint).</summary>
public sealed record SetupStep(string Title, string Description, bool Done, string? ActionText, string? ActionUrl);

public sealed class DashboardViewModel
{
    public required SubscriptionSnapshot Snapshot { get; init; }
    public required PortfolioTotals Totals { get; init; }
    public required DateTime NowUtc { get; init; }
    public required DateOnly Today { get; init; }
    public required bool CanManageSubscription { get; init; }
    public required bool CanEdit { get; init; }
    public required LeaseCounts LeaseCounts { get; init; }
    public required BillingSummary Billing { get; init; }
    public required CollectionSummary Collections { get; init; }
    public required IReadOnlyList<LeaseSummary> NeedingAttention { get; init; }
    public AgencyPanel? Agency { get; init; }
    public SalesSummaryTotals? Sales { get; init; }
    public required IReadOnlyList<SetupStep> Steps { get; init; }
    public string SetupTitle { get; init; } = "Set up your portfolio";
    /// <summary>Occupancy and rent roll: shown for landlords and agencies, and for developers only once they let units.</summary>
    public bool ShowRentalSummary { get; init; } = true;
    public IReadOnlyList<ProjectCard> Projects { get; init; } = [];
    /// <summary>Every property as a card (for sale and to let), by name.</summary>
    public IReadOnlyList<PropertyCardItem> PropertyCards { get; init; } = [];
    /// <summary>The "All properties to let" strip: counts and unit stats over properties to let only (null = use Totals).</summary>
    public int? ToLetCount { get; init; }
    public int NewLeads { get; init; }
    public int AwaitingConfirmation { get; init; }
    public RealEstateERP.Core.Models.UnitStats? RentalStats { get; init; }

    public int DoneCount => Steps.Count(s => s.Done);
    public int ProgressPercent => Steps.Count == 0 ? 0 : DoneCount * 100 / Steps.Count;
    public bool SetupComplete => DoneCount == Steps.Count;
}

/// <summary>Agency dashboard panel. For a restricted agent everything is already limited to their contracts.</summary>
public sealed record AgencyPanel(
    bool IsAgent,
    string? AgentName,
    DateOnly LastMonth,
    int ActiveContracts,
    int Properties,
    IReadOnlyList<ContractSummary> ToPrepare,
    int AwaitingPayment,
    decimal CollectedForOwners,
    decimal Commission);
