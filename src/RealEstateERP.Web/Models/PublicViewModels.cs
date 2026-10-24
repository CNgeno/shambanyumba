using RealEstateERP.Core.Entities;

namespace RealEstateERP.Web.Models;

public sealed class PricingViewModel
{
    public RealEstateERP.Core.Models.TariffTable? Tariff { get; init; }
    public required IReadOnlyList<SubscriptionPlan> Plans { get; init; }
    public required string TrialPlanCode { get; init; }
}

public sealed class ContactViewModel
{
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
