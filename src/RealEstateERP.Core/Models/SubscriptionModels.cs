using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

/// <summary>What the rest of the app needs to know about a business's subscription right now.</summary>
public sealed record SubscriptionSnapshot(
    SubscriptionStatus Status,
    string PlanName,
    string PlanCode,
    int? UnitLimit,
    int CreditsPerMonth,
    DateTime? PaidThroughUtc,
    DateTime? GraceEndsUtc,
    bool IsTrial)
{
    public bool AllowsAccess => Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.Grace;

    public static SubscriptionSnapshot None { get; } =
        new(SubscriptionStatus.Suspended, "No plan", string.Empty, 0, 0, null, null, false);

    public int DaysLeft(DateTime nowUtc)
        => PaidThroughUtc is DateTime end ? Math.Max(0, (int)Math.Ceiling((end - nowUtc).TotalDays)) : 0;
}

public sealed record UploadedFile(Stream Content, string FileName, long Length);

public sealed record RenewalRequest(
    int PlanId,
    int Months,
    SubscriptionPaymentMethod Method,
    string Reference,
    UploadedFile? Proof);

public sealed record SubscriptionPlanInput(
    int? Id,
    string Code,
    string Name,
    string? Description,
    int? UnitLimit,
    decimal MonthlyFee,
    int CreditsPerMonth,
    int TrialDays,
    bool IsActive,
    int SortOrder,
    BusinessType? ForBusinessType);
