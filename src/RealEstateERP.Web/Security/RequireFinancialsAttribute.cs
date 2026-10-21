using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Security;

/// <summary>Reports, costs and margins, expenses and net income: Owners, Admins, and members given "Can see finances".</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireFinancialsAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (await context.HttpContext.RequestServices.GetRequiredService<ICapabilityService>().CanViewFinancialsAsync(context.HttpContext.RequestAborted))
        {
            await next();
            return;
        }
        context.Result = new ViewResult
        {
            ViewName = "WrongBusinessType",
            ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Title"] = "Finances are private",
                ["Needed"] = "permission to see finances",
                ["How"] = "Reports, costs, expenses and income are visible to Owners, Admins and team members they allow. Ask an Owner or Admin (Business settings > Team)."
            }
        };
    }
}
