using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RealEstateERP.Web.Filters;

/// <summary>
/// Replaces the blank "400 Bad Request" of a failed antiforgery check on sign-in pages. It happens when a form is sent
/// with a token issued for a different user: a double tap on "Sign in" (the first tap already signed the person in),
/// or a sign-in page left open while they signed in or out in another tab.
/// Already signed in: carry on to the returnUrl. Otherwise: show the page again with a fresh form.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StaleFormRecoveryAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;

        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var url = context.HttpContext.RequestServices.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context);
            string? returnUrl = http.Request.Query["returnUrl"];
            context.Result = new LocalRedirectResult(!string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
            return;
        }

        var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        tempData["StatusMessage"] = "This page had expired, so nothing was sent. Please try again.";
        tempData.Save();   // the failed check skipped the filter that normally saves TempData
        context.Result = new RedirectResult(http.Request.PathBase + http.Request.Path + http.Request.QueryString);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
