using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Filters;
using RealEstateERP.Web.Formatting;

namespace RealEstateERP.Web.ViewComponents;

public sealed record SubscriptionBannerModel(string Tone, string Message, bool ShowRenewLink);

public sealed class SubscriptionBannerViewComponent(
    ICurrentUserService currentUser,
    ISubscriptionService subscriptions,
    TimeProvider clock) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (currentUser.BusinessId is not int businessId)
            return Content(string.Empty);

        var snapshot = HttpContext.Items[SubscriptionGateFilter.SnapshotItemKey] as SubscriptionSnapshot
            ?? await subscriptions.GetSnapshotAsync(businessId);

        var model = Build(snapshot, clock.GetUtcNow().UtcDateTime);
        return model is null ? Content(string.Empty) : View(model);
    }

    private static SubscriptionBannerModel? Build(SubscriptionSnapshot s, DateTime now)
    {
        var days = s.DaysLeft(now);
        var dayWord = days == 1 ? "day" : "days";

        return s.Status switch
        {
            SubscriptionStatus.Trial => new(days <= 7 ? "warning" : "info",
                $"Free trial: {days} {dayWord} left, ends {s.PaidThroughUtc.ToEatDate()}.", true),

            SubscriptionStatus.Active when days <= 5 => new("info",
                $"Your {s.PlanName} plan ends in {days} {dayWord} ({s.PaidThroughUtc.ToEatDate()}).", true),

            SubscriptionStatus.Grace => new("warning",
                $"Your subscription ended on {s.PaidThroughUtc.ToEatDate()}. Renew before {s.GraceEndsUtc.ToEatDate()} to keep access.", true),

            SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled => new("danger",
                "Your subscription has expired. Renew to continue using SmartBiz.", false),

            _ => null
        };
    }
}
