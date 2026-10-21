using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Security;

public enum Capability { Sales, Agency }

/// <summary>
/// Module pages that depend on what the business does: Sales needs a property for sale; Agency needs the agency
/// setting (and a plan that includes it). Others see an explanation of how to switch it on.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCapabilityAttribute(Capability capability) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var caps = await context.HttpContext.RequestServices.GetRequiredService<ICapabilityService>().GetAsync(context.HttpContext.RequestAborted);
        var ok = capability switch { Capability.Sales => caps.Sales, Capability.Agency => caps.Agency, _ => true };
        if (ok) { await next(); return; }

        var (needed, how) = capability switch
        {
            Capability.Sales => ("a property for sale", "Add a property and set its purpose to For sale (or To let and for sale), or change an existing property's purpose."),
            _ when caps.AgencyOn => ("a plan that includes agency features", "Managing for other owners comes with the Agent or Enterprise plan. Change plan under Subscription."),
            _ => ("agency features switched on", "Tick 'We manage properties for other owners' in Business settings.")
        };
        context.Result = new ViewResult
        {
            ViewName = "WrongBusinessType",
            ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Title"] = "Not set up for this yet",
                ["Needed"] = needed,
                ["How"] = how
            }
        };
    }
}
