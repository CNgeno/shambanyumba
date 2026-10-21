using RealEstateERP.Core.Entities;

namespace RealEstateERP.Web.Models;

public sealed class PricingViewModel
{
    public required IReadOnlyList<SubscriptionPlan> Plans { get; init; }
    public required string TrialPlanCode { get; init; }
}

public sealed class ContactViewModel
{
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
