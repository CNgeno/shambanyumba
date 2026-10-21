using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Security;

/// <summary>Module pages for one kind of business (Agency features, Developer sales). Others see an explanation.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireBusinessTypeAttribute(params BusinessType[] types) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var current = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>().BusinessType;
        if (current is BusinessType t && types.Contains(t)) return;

        context.Result = new ViewResult
        {
            ViewName = "WrongBusinessType",
            ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Title"] = "Not available for this business",
                ["Needed"] = string.Join(" or ", types),
                ["Current"] = current?.ToString() ?? "unknown"
            }
        };
    }
}
