using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Filters;

/// <summary>
/// NFR-06 / FR-02-B. Registered GLOBALLY: every controller action for a selected business is gated.
/// Trial/Active/Grace pass (Grace shows a banner); Suspended/Cancelled go to /Subscription/Renew.
/// </summary>
public sealed class SubscriptionGateFilter(ICurrentUserService currentUser, ISubscriptionService subscriptions) : IAsyncActionFilter
{
    public const string SnapshotItemKey = "SubscriptionSnapshot";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;

        if (currentUser.BusinessId is not int businessId
            || metadata.OfType<IAllowAnonymous>().Any()
            || metadata.OfType<AllowWithoutBusinessAttribute>().Any()
            || metadata.OfType<SkipSubscriptionCheckAttribute>().Any())
        {
            await next();
            return;
        }

        var snapshot = await subscriptions.GetSnapshotAsync(businessId, context.HttpContext.RequestAborted);
        context.HttpContext.Items[SnapshotItemKey] = snapshot;

        if (snapshot.AllowsAccess)
        {
            await next();
            return;
        }

        const string renewPath = "/Subscription/Renew";
        if (context.HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            context.HttpContext.Response.Headers["X-Redirect-To"] = renewPath;
            context.Result = new StatusCodeResult(StatusCodes.Status402PaymentRequired);
            return;
        }
        context.Result = new RedirectResult(renewPath);
    }
}
